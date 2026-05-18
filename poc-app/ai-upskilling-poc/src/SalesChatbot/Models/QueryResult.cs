namespace SalesChatbot.Models;

public sealed class QueryResult
{
    public required IReadOnlyList<string> ColumnNames { get; init; }

    public required IReadOnlyList<IReadOnlyDictionary<string, object?>> Rows { get; init; }

    public int RowCount => Rows.Count;
}
