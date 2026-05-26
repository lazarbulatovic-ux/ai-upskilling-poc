namespace SalesChatbot.Models;

public sealed record SqlValidationResult(bool IsApproved, string? RejectionReason = null)
{
    public static SqlValidationResult Approved() => new(true);

    public static SqlValidationResult Rejected(string reason) => new(false, reason);
}
