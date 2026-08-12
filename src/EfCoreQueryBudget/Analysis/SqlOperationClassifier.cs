using System.Text.RegularExpressions;

namespace EfCoreQueryBudget;

/// <summary>
/// Decides whether a command reads or writes, from its leading keyword.
/// </summary>
internal static partial class SqlOperationClassifier
{
    private static readonly string[] WriteKeywords =
        ["INSERT", "UPDATE", "DELETE", "MERGE", "TRUNCATE"];

    public static QueryOperation Classify(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return QueryOperation.Other;
        }

        var keyword = LeadingKeyword(sql);

        if (keyword.Equals("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            return QueryOperation.Read;
        }

        // A CTE reads unless it ends in a write, which PostgreSQL and SQL Server both allow.
        if (keyword.Equals("WITH", StringComparison.OrdinalIgnoreCase))
        {
            return WriteKeywordRegex().IsMatch(sql) ? QueryOperation.Write : QueryOperation.Read;
        }

        foreach (var write in WriteKeywords)
        {
            if (keyword.Equals(write, StringComparison.OrdinalIgnoreCase))
            {
                return QueryOperation.Write;
            }
        }

        return QueryOperation.Other;
    }

    /// <summary>
    /// The first word, past any leading comments. EF Core writes query tags as <c>--</c> comments
    /// ahead of the statement, so a classifier that looked at the first character would misread
    /// every tagged query.
    /// </summary>
    private static ReadOnlySpan<char> LeadingKeyword(string sql)
    {
        var index = 0;

        while (index < sql.Length)
        {
            if (char.IsWhiteSpace(sql[index]))
            {
                index++;
                continue;
            }

            if (sql[index] == '-' && CharAt(sql, index + 1) == '-')
            {
                while (index < sql.Length && sql[index] is not ('\n' or '\r'))
                {
                    index++;
                }

                continue;
            }

            if (sql[index] == '/' && CharAt(sql, index + 1) == '*')
            {
                var close = sql.IndexOf("*/", index + 2, StringComparison.Ordinal);
                index = close < 0 ? sql.Length : close + 2;
                continue;
            }

            // Some providers wrap a statement in parentheses.
            if (sql[index] == '(')
            {
                index++;
                continue;
            }

            break;
        }

        var start = index;
        while (index < sql.Length && char.IsLetter(sql[index]))
        {
            index++;
        }

        return sql.AsSpan(start, index - start);
    }

    private static char CharAt(string sql, int index)
        => index < sql.Length ? sql[index] : '\0';

    [GeneratedRegex(
        @"\b(INSERT|UPDATE|DELETE|MERGE)\b",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex WriteKeywordRegex();
}
