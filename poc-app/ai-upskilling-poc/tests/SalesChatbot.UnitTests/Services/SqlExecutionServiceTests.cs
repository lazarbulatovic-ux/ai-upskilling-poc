using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using SalesChatbot.Data;
using SalesChatbot.Services;
using SalesChatbot.Services.Validation;

namespace SalesChatbot.UnitTests.Services;

/// <summary>
/// Unit tests for SqlExecutionService covering the validation path.
/// Full execution-path tests require a database and live in SalesChatbot.IntegrationTests.
/// </summary>
public class SqlExecutionServiceTests
{
    private static SqlExecutionService BuildSut()
    {
        // Use a non-connected context; validation throws before any DB access is attempted.
        var options = new DbContextOptionsBuilder<SalesDbContext>()
            .UseSqlServer("Server=.;Database=Test;Trusted_Connection=True;")
            .Options;
        var dbContext = new SalesDbContext(options);
        return new SqlExecutionService(dbContext);
    }

    [Fact]
    public async Task ExecuteQueryAsync_InvalidSql_ThrowsInvalidOperationException()
    {
        var sut = BuildSut();

        var act = () => sut.ExecuteQueryAsync("DELETE FROM Orders");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteQueryAsync_MultiStatement_ThrowsInvalidOperationException()
    {
        var sut = BuildSut();

        var act = () => sut.ExecuteQueryAsync("SELECT 1; DROP TABLE Orders");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteQueryAsync_CannotAnswerSentinel_ThrowsInvalidOperationException()
    {
        var sut = BuildSut();

        var act = () => sut.ExecuteQueryAsync("CANNOT_ANSWER");

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task ExecuteQueryAsync_EmptySql_ThrowsInvalidOperationException()
    {
        var sut = BuildSut();

        var act = () => sut.ExecuteQueryAsync(string.Empty);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
