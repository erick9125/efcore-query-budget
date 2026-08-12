using System.Text;
using System.Text.RegularExpressions;

namespace EfCoreQueryBudget;

/// <summary>
/// Replaces inline SQL literals with <c>?</c> and collapses variable-length <c>IN</c> lists, so
/// that executions differing only in a literal share one structural fingerprint.
/// </summary>
/// <remarks>
/// A hand-written single-pass scanner rather than a set of regular expressions: quoted identifiers,
/// escaped quotes and comments all have to be tracked as regions, and a pattern that ignores them
/// desynchronizes on the first apostrophe inside a comment.
/// </remarks>
internal static partial class SqlLiteralMasker
{
    private const char Placeholder = '?';

    public static string Mask(string sql)
    {
        var output = new StringBuilder(sql.Length);
        var index = 0;

        while (index < sql.Length)
        {
            var current = sql[index];

            if (current == '-' && CharAt(sql, index + 1) == '-')
            {
                index = CopyLineComment(sql, index, output);
            }
            else if (current == '/' && CharAt(sql, index + 1) == '*')
            {
                index = CopyBlockComment(sql, index, output);
            }
            else if (current is '"' or '`')
            {
                index = CopyDelimited(sql, index, output, current);
            }
            else if (current == '[')
            {
                index = CopyDelimited(sql, index, output, ']');
            }
            else if (current == '\'')
            {
                index = MaskQuoted(sql, index, output, backslashEscapes: false);
            }
            else if (current == '$')
            {
                index = CopyPositionalParameterOrMaskDollarQuoted(sql, index, output);
            }
            else if ((current is '@' or ':') && IsIdentifierPart(CharAt(sql, index + 1)))
            {
                index = CopyParameter(sql, index, output);
            }
            else if (IsIdentifierStart(current))
            {
                index = CopyWordOrMaskPrefixedLiteral(sql, index, output);
            }
            else if (IsDigit(current) || (current == '.' && IsDigit(CharAt(sql, index + 1))))
            {
                index = MaskNumber(sql, index, output);
            }
            else if (char.IsWhiteSpace(current))
            {
                AppendSeparator(output);
                index = SkipWhitespace(sql, index);
            }
            else
            {
                output.Append(current);
                index++;
            }
        }

        return CollapseInLists(output.ToString().Trim());
    }

    private static char CharAt(string sql, int index)
        => index < sql.Length ? sql[index] : '\0';

    /// <summary>
    /// Copies a <c>--</c> comment verbatim and keeps its terminating newline: collapsing that
    /// newline into a space would swallow the rest of the statement into the comment.
    /// </summary>
    private static int CopyLineComment(string sql, int index, StringBuilder output)
    {
        while (index < sql.Length && sql[index] is not ('\n' or '\r'))
        {
            output.Append(sql[index]);
            index++;
        }

        output.Append('\n');
        return SkipWhitespace(sql, index);
    }

    private static int CopyBlockComment(string sql, int index, StringBuilder output)
    {
        output.Append("/*");
        index += 2;

        while (index < sql.Length)
        {
            if (sql[index] == '*' && CharAt(sql, index + 1) == '/')
            {
                output.Append("*/");
                return index + 2;
            }

            output.Append(sql[index]);
            index++;
        }

        return index;
    }

    /// <summary>
    /// Copies a quoted identifier verbatim. It is still scanned rather than skipped so that a
    /// quote inside it cannot desynchronize the caller.
    /// </summary>
    private static int CopyDelimited(string sql, int index, StringBuilder output, char close)
    {
        output.Append(sql[index]);
        index++;

        while (index < sql.Length)
        {
            var current = sql[index];
            output.Append(current);
            index++;

            if (current != close)
            {
                continue;
            }

            // A doubled closing delimiter is an escaped one; the identifier continues.
            if (CharAt(sql, index) == close)
            {
                output.Append(close);
                index++;
                continue;
            }

            return index;
        }

        return index;
    }

    private static int MaskQuoted(string sql, int index, StringBuilder output, bool backslashEscapes)
    {
        output.Append(Placeholder);
        index++;

        while (index < sql.Length)
        {
            if (backslashEscapes && sql[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (sql[index] != '\'')
            {
                index++;
                continue;
            }

            index++;
            if (CharAt(sql, index) == '\'')
            {
                index++;
                continue;
            }

            return index;
        }

        return index;
    }

    private static int CopyPositionalParameterOrMaskDollarQuoted(
        string sql,
        int index,
        StringBuilder output)
    {
        // $1, $2 — PostgreSQL positional parameters. Copied so their digits are not masked.
        if (IsDigit(CharAt(sql, index + 1)))
        {
            output.Append('$');
            index++;
            while (index < sql.Length && IsDigit(sql[index]))
            {
                output.Append(sql[index]);
                index++;
            }

            return index;
        }

        // $$…$$ or $tag$…$tag$ — dollar-quoted string.
        var tagEnd = index + 1;
        while (tagEnd < sql.Length && IsIdentifierPart(sql[tagEnd]))
        {
            tagEnd++;
        }

        if (CharAt(sql, tagEnd) == '$')
        {
            var tag = sql[index..(tagEnd + 1)];
            var close = sql.IndexOf(tag, tagEnd + 1, StringComparison.Ordinal);
            output.Append(Placeholder);
            return close < 0 ? sql.Length : close + tag.Length;
        }

        output.Append('$');
        return index + 1;
    }

    private static int CopyParameter(string sql, int index, StringBuilder output)
    {
        output.Append(sql[index]);
        index++;

        while (index < sql.Length && IsIdentifierPart(sql[index]))
        {
            output.Append(sql[index]);
            index++;
        }

        return index;
    }

    /// <summary>
    /// Copies an identifier or keyword verbatim, which is what keeps <c>NULL</c>, <c>TRUE</c> and
    /// identifiers such as <c>table1</c> intact. A single-letter word introducing a quoted literal
    /// (<c>N'…'</c>, <c>E'…'</c>, <c>X'…'</c>, <c>B'…'</c>) is consumed together with it instead.
    /// </summary>
    private static int CopyWordOrMaskPrefixedLiteral(string sql, int index, StringBuilder output)
    {
        var start = index;
        while (index < sql.Length && IsIdentifierPart(sql[index]))
        {
            index++;
        }

        var word = sql.AsSpan(start, index - start);
        if (word.Length == 1 && CharAt(sql, index) == '\'')
        {
            switch (word[0])
            {
                // PostgreSQL escape strings honour backslash escapes; the others do not.
                case 'E' or 'e':
                    return MaskQuoted(sql, index, output, backslashEscapes: true);
                case 'N' or 'n' or 'X' or 'x' or 'B' or 'b':
                    return MaskQuoted(sql, index, output, backslashEscapes: false);
            }
        }

        output.Append(word);
        return index;
    }

    private static int MaskNumber(string sql, int index, StringBuilder output)
    {
        output.Append(Placeholder);

        if (sql[index] == '0' && CharAt(sql, index + 1) is 'x' or 'X')
        {
            index += 2;
            while (index < sql.Length && Uri.IsHexDigit(sql[index]))
            {
                index++;
            }

            return index;
        }

        index = SkipDigits(sql, index);

        if (CharAt(sql, index) == '.')
        {
            index = SkipDigits(sql, index + 1);
        }

        if (CharAt(sql, index) is 'e' or 'E')
        {
            var exponent = index + 1;
            if (CharAt(sql, exponent) is '+' or '-')
            {
                exponent++;
            }

            if (IsDigit(CharAt(sql, exponent)))
            {
                index = SkipDigits(sql, exponent);
            }
        }

        return index;
    }

    private static void AppendSeparator(StringBuilder output)
    {
        if (output.Length > 0 && output[^1] is not (' ' or '\n'))
        {
            output.Append(' ');
        }
    }

    private static int SkipWhitespace(string sql, int index)
    {
        while (index < sql.Length && char.IsWhiteSpace(sql[index]))
        {
            index++;
        }

        return index;
    }

    private static int SkipDigits(string sql, int index)
    {
        while (index < sql.Length && IsDigit(sql[index]))
        {
            index++;
        }

        return index;
    }

    private static bool IsIdentifierStart(char value)
        => char.IsLetter(value) || value is '_' or '#';

    private static bool IsIdentifierPart(char value)
        => char.IsLetterOrDigit(value) || value is '_' or '#';

    private static bool IsDigit(char value)
        => value is >= '0' and <= '9';

    /// <summary>
    /// Collapses <c>IN (?, ?, ?)</c> and <c>IN (@p0, @p1, @p2)</c> to <c>IN (?)</c>, so a
    /// variable-length list is one pattern. The preceding <c>IN</c> is required on purpose:
    /// a <c>VALUES (?, ?)</c> list must not collapse, since a different column count is a
    /// genuinely different query shape.
    /// </summary>
    private static string CollapseInLists(string sql)
        => InListRegex().Replace(sql, "$1 (?)");

    [GeneratedRegex(
        @"\b(IN)\s*\(\s*(?:\?|@\w+|:\w+|\$\d+)(?:\s*,\s*(?:\?|@\w+|:\w+|\$\d+))+\s*\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InListRegex();
}
