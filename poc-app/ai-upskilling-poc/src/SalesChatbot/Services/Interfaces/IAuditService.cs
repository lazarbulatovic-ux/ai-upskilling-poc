using SalesChatbot.Data.Entities;

namespace SalesChatbot.Services.Interfaces;

public interface IAuditService
{
    Task LogAsync(QueryAuditEntry entry, CancellationToken cancellationToken = default);
}
