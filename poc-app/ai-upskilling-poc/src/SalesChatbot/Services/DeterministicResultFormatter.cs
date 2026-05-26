using System.Text;
using System.Text.RegularExpressions;
using SalesChatbot.Models;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Services;

public sealed partial class DeterministicResultFormatter
{
    private static readonly HashSet<string> CurrencyColumnKeywords =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "revenue", "price", "total", "amount", "spent", "cost"
        };

    public Task<string> InterpretAsync(
        string userQuestion,
        QueryResult queryResult,
        IReadOnlyList<ChatExchange> history,
        CancellationToken cancellationToken = default)
    {
        var result = queryResult.RowCount == 0
            ? FormatZeroRows()
            : queryResult.RowCount == 1 && queryResult.ColumnNames.Count == 1
                ? FormatSingleValue(queryResult)
                : FormatTable(queryResult);

        return Task.FromResult(result);
    }

    private static string FormatZeroRows() =>
        "No results were found matching your query.";

    private static string FormatSingleValue(QueryResult queryResult)
    {
        var columnName = queryResult.ColumnNames[0];
        var value = queryResult.Rows[0][columnName];
        var label = HumaniseHeader(columnName);
        var formatted = FormatValue(columnName, value);
        return $"{label}: {formatted}";
    }

    private static string FormatTable(QueryResult queryResult)
    {
        var sb = new StringBuilder();
        var columns = queryResult.ColumnNames;

        // Header row
        sb.Append('|');
        foreach (var col in columns)
        {
            sb.Append($" {HumaniseHeader(col)} |");
        }
        sb.AppendLine();

        // Separator row
        sb.Append('|');
        foreach (var _ in columns)
        {
            sb.Append("---|");
        }
        sb.AppendLine();

        // Data rows — ALL rows, no Take()
        foreach (var row in queryResult.Rows)
        {
            sb.Append('|');
            foreach (var col in columns)
            {
                var val = row.TryGetValue(col, out var v) ? v : null;
                sb.Append($" {FormatValue(col, val)} |");
            }
            sb.AppendLine();
        }

        return sb.ToString().TrimEnd();
    }

    public static string HumaniseHeader(string columnName)
    {
        // Insert space before uppercase letters that follow lowercase letters (PascalCase → Title Case)
        var result = PascalCaseRegex().Replace(columnName, "$1 $2");
        // Also handle sequences like "SQLQuery" → "SQL Query"
        result = UpperSequenceRegex().Replace(result, "$1 $2");
        return result.Trim();
    }

    public static string FormatValue(string columnName, object? value)
    {
        if (value is null)
        {
            return string.Empty;
        }

        if (value is DateTime dt)
        {
            return dt.ToString("d MMM yyyy");
        }

        if (value is DateTimeOffset dto)
        {
            return dto.ToString("d MMM yyyy");
        }

        if (IsCurrencyColumn(columnName) && TryGetDecimal(value, out var amount))
        {
            return $"€{amount:N2}";
        }

        if (value is decimal or double or float)
        {
            return TryGetDecimal(value, out var num) ? num.ToString("N2") : value.ToString()!;
        }

        return value.ToString() ?? string.Empty;
    }

    public static string Format(QueryResult queryResult)
    {
        if (queryResult.RowCount == 0)
            return FormatZeroRows();

        if (queryResult.RowCount == 1 && queryResult.ColumnNames.Count == 1)
            return FormatSingleValue(queryResult);

        return FormatTable(queryResult);
    }

    private static bool IsCurrencyColumn(string columnName)
    {
        var lower = columnName.ToLowerInvariant();
        return CurrencyColumnKeywords.Any(kw => lower.Contains(kw));
    }

    private static bool TryGetDecimal(object value, out decimal result)
    {
        result = 0;
        return value switch
        {
            decimal d => (result = d) is var _ && true,
            double dbl => (result = (decimal)dbl) is var _ && true,
            float f => (result = (decimal)f) is var _ && true,
            int i => (result = i) is var _ && true,
            long l => (result = l) is var _ && true,
            _ => false
        };
    }

    [GeneratedRegex(@"([a-z])([A-Z])")]
    private static partial Regex PascalCaseRegex();

    [GeneratedRegex(@"([A-Z]+)([A-Z][a-z])")]
    private static partial Regex UpperSequenceRegex();
}
