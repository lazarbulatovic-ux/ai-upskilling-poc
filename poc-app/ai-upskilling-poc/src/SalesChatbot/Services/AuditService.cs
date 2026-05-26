using SalesChatbot.Data;
using SalesChatbot.Data.Entities;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Services;

public sealed class AuditService(SalesDbContext dbContext, ILogger<AuditService> logger) : IAuditService
{
    public async Task LogAsync(QueryAuditEntry entry, CancellationToken cancellationToken = default)
    {
        try
        {
            dbContext.QueryAuditLog.Add(entry);
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Audit] Failed to persist audit entry for question: {Question}", entry.UserQuestion);
        }
    }
}
