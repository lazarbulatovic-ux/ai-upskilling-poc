using System.Text.RegularExpressions;
using SalesChatbot.Services;

namespace SalesChatbot.Services.Validation;

public static partial class SqlSafetyValidator
{
    private static readonly string[] ForbiddenTokens =
    [
        "INSERT", "UPDATE", "DELETE", "DROP", "CREATE", "ALTER", "TRUNCATE",
        "MERGE", "EXEC", "EXECUTE", "ERASE", "GRANT", "REVOKE", "INTO", "OPENROWSET",
        "OPENQUERY", "XP_", "SP_"
    ];

    public static bool IsValidSelect(string? sql, out string? failureReason)
    {
        failureReason = null;

        if (string.IsNullOrWhiteSpace(sql))
        {
            failureReason = "SQL is empty.";
            return false;
        }

        var trimmed = sql.Trim();

        if (trimmed.Equals(ChatConstants.CannotAnswer, StringComparison.Ordinal))
        {
            failureReason = "Sentinel is not executable SQL.";
            return false;
        }

        if (trimmed.Contains(';', StringComparison.Ordinal))
        {
            failureReason = "Multiple statements are not allowed.";
            return false;
        }

        var withoutComments = StripComments(trimmed);
        if (!withoutComments.StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            failureReason = "Only SELECT statements are allowed.";
            return false;
        }

        var upper = withoutComments.ToUpperInvariant();
        foreach (var token in ForbiddenTokens)
        {
            if (ContainsWholeWord(upper, token))
            {
                failureReason = $"Forbidden token '{token}' detected.";
                return false;
            }
        }

        return true;
    }

    public static bool ContainsBlockedKeyword(string? sql, out string? matchedKeyword)
    {
        matchedKeyword = null;

        if (string.IsNullOrWhiteSpace(sql))
        {
            return false;
        }

        var upper = StripComments(sql).ToUpperInvariant();
        foreach (var token in ForbiddenTokens)
        {
            if (ContainsWholeWord(upper, token))
            {
                matchedKeyword = token;
                return true;
            }
        }

        return false;
    }

    public static string EnforceRowLimit(string sql, int maxRows = 500)
    {
        var withoutComments = StripComments(sql.Trim());
        var match = TopRegex().Match(withoutComments);
        if (!match.Success)
        {
            return TopInsertRegex().Replace(withoutComments, m =>
                $"{m.Groups[1].Value} TOP {maxRows} ", 1);
        }

        if (int.TryParse(match.Groups[1].Value, out var top) && top > maxRows)
        {
            return TopRegex().Replace(withoutComments, $"TOP {maxRows}", 1);
        }

        return withoutComments;
    }

    private static string StripComments(string sql)
    {
        var noBlock = BlockCommentRegex().Replace(sql, " ");
        return LineCommentRegex().Replace(noBlock, " ").Trim();
    }

    private static bool ContainsWholeWord(string text, string word) =>
        Regex.IsMatch(text, $@"\b{Regex.Escape(word)}\b", RegexOptions.CultureInvariant);

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex BlockCommentRegex();

    [GeneratedRegex(@"--[^\r\n]*")]
    private static partial Regex LineCommentRegex();

    [GeneratedRegex(@"\bTOP\s+(\d+)\b", RegexOptions.IgnoreCase)]
    private static partial Regex TopRegex();

    [GeneratedRegex(@"(\bSELECT\s+(?:DISTINCT\s+)?)", RegexOptions.IgnoreCase)]
    private static partial Regex TopInsertRegex();
}
