namespace SalesChatbot.Data.Entities;

public sealed class QueryAuditEntry
{
    public int Id { get; set; }

    public DateTime TimestampUtc { get; set; }

    public required string UserQuestion { get; set; }

    public required string GeneratedSql { get; set; }

    public bool WasBlocked { get; set; }

    public int RowCount { get; set; }

    public long ExecutionMs { get; set; }
}
