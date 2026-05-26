using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SalesChatbot.Data;
using SalesChatbot.Data.Entities;
using SalesChatbot.Services;

namespace SalesChatbot.UnitTests.Services;

public class AuditServiceTests
{
    private static SalesDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new SalesDbContext(options);
    }

    [Fact]
    public async Task LogAsync_ValidEntry_PersistsToDatabase()
    {
        await using var db = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<AuditService>>();
        var sut = new AuditService(db, logger);

        var entry = new QueryAuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            UserQuestion = "How many orders?",
            GeneratedSql = "SELECT COUNT(*) FROM Orders",
            WasBlocked = false,
            RowCount = 1,
            ExecutionMs = 12
        };

        await sut.LogAsync(entry);

        var saved = await db.QueryAuditLog.SingleAsync();
        saved.UserQuestion.Should().Be("How many orders?");
        saved.GeneratedSql.Should().Be("SELECT COUNT(*) FROM Orders");
        saved.WasBlocked.Should().BeFalse();
        saved.RowCount.Should().Be(1);
        saved.ExecutionMs.Should().Be(12);
    }

    [Fact]
    public async Task LogAsync_BlockedEntry_PersistsWithWasBlockedTrue()
    {
        await using var db = CreateInMemoryContext();
        var logger = Substitute.For<ILogger<AuditService>>();
        var sut = new AuditService(db, logger);

        var entry = new QueryAuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            UserQuestion = "Drop all orders",
            GeneratedSql = "DELETE FROM Orders",
            WasBlocked = true,
            RowCount = 0,
            ExecutionMs = 0
        };

        await sut.LogAsync(entry);

        var saved = await db.QueryAuditLog.SingleAsync();
        saved.WasBlocked.Should().BeTrue();
        saved.RowCount.Should().Be(0);
        saved.ExecutionMs.Should().Be(0);
    }

    [Fact]
    public async Task LogAsync_WhenSaveThrows_DoesNotPropagate()
    {
        // Use a disposed context to force SaveChangesAsync to throw
        var db = CreateInMemoryContext();
        await db.DisposeAsync();

        var logger = Substitute.For<ILogger<AuditService>>();
        var sut = new AuditService(db, logger);

        var entry = new QueryAuditEntry
        {
            TimestampUtc = DateTime.UtcNow,
            UserQuestion = "test",
            GeneratedSql = "SELECT 1",
            WasBlocked = false,
            RowCount = 0,
            ExecutionMs = 0
        };

        var act = async () => await sut.LogAsync(entry);

        await act.Should().NotThrowAsync("audit failures must never break the chatbot response");
    }
}
