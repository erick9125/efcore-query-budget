using System.Globalization;
using System.Text;

namespace ErickMorales.EntityFrameworkCore.QueryBudget;

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

        foreach (var violation in result.Violations)
        {
            lines.AppendLine(violation.Label);
            lines.AppendLine($"  Budget: <= {FormatBudgetValue(violation)}");
            lines.AppendLine($"  Actual:   {FormatActualValue(violation)}");
            lines.AppendLine();
        }

        AppendGroups(
            lines,
            "Repeated query pattern",
            result.Metrics.RepeatedPatternGroups,
            possibleNPlusOne: true,
            result.Budget.ParameterDisplayMode);

        AppendGroups(
            lines,
            "Repeated exact query",
            result.Metrics.ExactDuplicateGroups,
            possibleNPlusOne: false,
            result.Budget.ParameterDisplayMode);

        if (result.Budget.ParameterDisplayMode == QueryParameterDisplayMode.Hidden)
        {
            lines.AppendLine(
                "Parameter values are hidden. Set ParameterDisplayMode to TypesOnly or Full to show them (they may contain tokens or personal data).");
        }

        return lines.ToString().TrimEnd();
    }

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
            lines.AppendLine($"Distinct parameter sets: {group.DistinctParameterSetCount}");

            if (possibleNPlusOne && group.DistinctParameterSetCount > 1)
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

    private static string FormatBudgetValue(QueryBudgetViolation violation)
    {
        return violation.Budget switch
        {
            TimeSpan ts => $"{ts.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)} ms",
            _ => Convert.ToString(violation.Budget, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

    private static string FormatActualValue(QueryBudgetViolation violation)
    {
        return violation.Actual switch
        {
            TimeSpan ts => $"{ts.TotalMilliseconds.ToString("0.###", CultureInfo.InvariantCulture)} ms",
            _ => Convert.ToString(violation.Actual, CultureInfo.InvariantCulture) ?? string.Empty
        };
    }

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
            byte[] bytes => $"byte[{bytes.Length}]",
            _ => value.GetType().Name
        };
    }

    private static string FormatFullValue(object? value)
    {
        return value switch
        {
            null => "null",
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
