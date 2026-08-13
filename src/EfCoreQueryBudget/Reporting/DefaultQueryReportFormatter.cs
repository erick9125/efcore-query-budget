using System.Globalization;
using System.Text;

namespace EfCoreQueryBudget;

public sealed class DefaultQueryReportFormatter : IQueryReportFormatter
{
    private const int MaxGroupsPerSection = 3;
    private const int MaxSqlLength = 300;

    public string Format(QueryBudgetResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        var lines = new StringBuilder();
        lines.AppendLine(result.Passed
            ? "EF Core query budget met"
            : "EF Core query budget exceeded");

        if (!string.IsNullOrWhiteSpace(result.Budget.ScopeLabel))
        {
            lines.AppendLine($"Scope: {result.Budget.ScopeLabel}");
        }

        lines.AppendLine();

        if (result.Metrics.DuplicateCaptureCount > 0)
        {
            lines.AppendLine(
                $"Discarded {result.Metrics.DuplicateCaptureCount} duplicate capture(s) of the same command execution. "
                + "The interceptor is attached to the DbContext more than once — check for a repeated AddInterceptors "
                + "registration, for example a test host that re-registers AddDbContext on top of the application's own.");
            lines.AppendLine();
        }

        if (result.Metrics.DiscardedQueryCount > 0)
        {
            lines.AppendLine(
                $"{result.Metrics.DiscardedQueryCount} query(s) ran but were not retained: the scope reached "
                + "MaxRecordedQueries. Counts and durations above cover every command; the duplicate and "
                + "pattern groups below cover the retained ones only. Raise MaxRecordedQueries, or set it to "
                + "null, to analyze them all.");
            lines.AppendLine();
        }

        foreach (var violation in result.Violations)
        {
            var (budget, actual) = FormatValues(violation);
            lines.AppendLine(Label(violation.Type));
            lines.AppendLine($"  Budget: <= {budget}");
            lines.AppendLine($"  Actual:   {actual}");
            lines.AppendLine();
        }

        AppendGroups(
            lines,
            "Repeated query pattern",
            Reads(result.Metrics.RepeatedPatternGroups),
            possibleNPlusOne: true,
            result.Budget.ParameterDisplayMode);

        AppendGroups(
            lines,
            "Repeated exact query",
            Reads(result.Metrics.ExactDuplicateGroups),
            possibleNPlusOne: false,
            result.Budget.ParameterDisplayMode);

        // Writes are shown but never called an N+1: a bulk insert has the same shape as one and is
        // not a defect. They do not count against the budget either.
        AppendGroups(
            lines,
            "Repeated write (not counted against the budget)",
            NonReads(result.Metrics.RepeatedPatternGroups),
            possibleNPlusOne: false,
            result.Budget.ParameterDisplayMode);

        AppendGroups(
            lines,
            "Repeated exact write (not counted against the budget)",
            NonReads(result.Metrics.ExactDuplicateGroups),
            possibleNPlusOne: false,
            result.Budget.ParameterDisplayMode);

        if (result.Budget.ParameterDisplayMode == QueryParameterDisplayMode.Hidden)
        {
            lines.AppendLine(
                "Parameter values are hidden. Set ParameterDisplayMode to TypesOnly or Full to show them (they may contain tokens or personal data).");
        }

        return lines.ToString().TrimEnd();
    }

    private static QueryGroup[] Reads(IReadOnlyList<QueryGroup> groups)
        => groups.Where(g => g.Operation == QueryOperation.Read).ToArray();

    private static QueryGroup[] NonReads(IReadOnlyList<QueryGroup> groups)
        => groups.Where(g => g.Operation != QueryOperation.Read).ToArray();

    private static void AppendGroups(
        StringBuilder lines,
        string title,
        IReadOnlyList<QueryGroup> groups,
        bool possibleNPlusOne,
        QueryParameterDisplayMode displayMode)
    {
        if (groups.Count == 0)
        {
            return;
        }

        foreach (var group in groups.Take(MaxGroupsPerSection))
        {
            lines.AppendLine(title);
            lines.AppendLine(Truncate(group.NormalizedSql, MaxSqlLength));
            lines.AppendLine($"Executions: {group.ExecutionCount}");
            lines.AppendLine($"Distinct variants: {group.DistinctVariantCount}");

            if (possibleNPlusOne && group.DistinctVariantCount > 1)
            {
                lines.AppendLine();
                lines.AppendLine("Possible N+1 query pattern.");
            }

            if (displayMode != QueryParameterDisplayMode.Hidden && group.Queries.Count > 0)
            {
                lines.AppendLine($"Sample parameters: {FormatParameters(group.Queries[0].Parameters, displayMode)}");
            }

            lines.AppendLine();
        }

        var hidden = groups.Count - MaxGroupsPerSection;
        if (hidden > 0)
        {
            lines.AppendLine($"... and {hidden} more group{(hidden == 1 ? string.Empty : "s")}");
            lines.AppendLine();
        }
    }

    /// <summary>
    /// How a violation reads. It belongs here rather than on the violation itself: the evaluator
    /// decides whether a limit was broken, not how to say so.
    /// </summary>
    private static string Label(QueryBudgetViolationType type)
    {
        return type switch
        {
            QueryBudgetViolationType.QueryCountExceeded => "Query count",
            QueryBudgetViolationType.ExactDuplicatesExceeded => "Exact duplicates",
            QueryBudgetViolationType.RepeatedPatternsExceeded => "Repeated query patterns",
            QueryBudgetViolationType.PatternExecutionsExceeded => "Executions in a single pattern",
            QueryBudgetViolationType.SlowQueriesExceeded => "Slow queries",
            QueryBudgetViolationType.TotalDurationExceeded => "Total database time",
            QueryBudgetViolationType.SingleQueryDurationExceeded => "Single query duration",
            _ => type.ToString()
        };
    }

    private static (string Budget, string Actual) FormatValues(QueryBudgetViolation violation)
    {
        return violation switch
        {
            CountBudgetViolation count => (
                count.Budget.ToString(CultureInfo.InvariantCulture),
                count.Actual.ToString(CultureInfo.InvariantCulture)),
            DurationBudgetViolation duration => (
                Milliseconds(duration.Budget),
                Milliseconds(duration.Actual)),
            _ => throw new ArgumentOutOfRangeException(
                nameof(violation),
                violation.GetType().Name,
                "Unknown budget violation kind.")
        };
    }

    private static string Milliseconds(TimeSpan value)
        => $"{value.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)} ms";

    private static string FormatParameters(
        IReadOnlyDictionary<string, object?> parameters,
        QueryParameterDisplayMode mode)
    {
        if (parameters.Count == 0)
        {
            return "(none)";
        }

        if (mode == QueryParameterDisplayMode.TypesOnly)
        {
            return string.Join(
                ", ",
                parameters.OrderBy(p => p.Key, StringComparer.Ordinal)
                    .Select(p => $"{p.Key}: {DescribeType(p.Value)}"));
        }

        return string.Join(
            ", ",
            parameters.OrderBy(p => p.Key, StringComparer.Ordinal)
                .Select(p => $"{p.Key}={FormatFullValue(p.Value)}"));
    }

    private static string DescribeType(object? value)
    {
        return value switch
        {
            null => "null",
            ParameterSnapshot snapshot => snapshot.TypeName,
            byte[] bytes => $"byte[{bytes.Length}]",
            _ => value.GetType().Name
        };
    }

    private static string FormatFullValue(object? value)
    {
        return value switch
        {
            null => "null",
            // A snapshot with no text is one that must never be printed, such as a binary payload.
            ParameterSnapshot snapshot => snapshot.Text ?? $"{snapshot.TypeName}#redacted",
            byte[] bytes => $"byte[{bytes.Length}]#redacted",
            string s => $"\"{s}\"",
            _ => Convert.ToString(value, CultureInfo.InvariantCulture) ?? value.GetType().Name
        };
    }

    private static string Truncate(string value, int maximum)
    {
        return value.Length <= maximum ? value : $"{value[..maximum]}...";
    }
}
