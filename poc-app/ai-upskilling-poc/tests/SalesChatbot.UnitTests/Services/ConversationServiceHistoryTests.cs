using FluentAssertions;
using NSubstitute;
using SalesChatbot.Models;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Services;

public class ConversationServiceHistoryTests
{
    private readonly ITextToSqlService _textToSql = Substitute.For<ITextToSqlService>();
    private readonly ISqlExecutionService _sqlExecution = Substitute.For<ISqlExecutionService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ConversationService _sut;

    public ConversationServiceHistoryTests()
    {
        _sut = new ConversationService(_textToSql, _sqlExecution, _auditService);
        SetupSuccessfulTurn();
    }

    private void SetupSuccessfulTurn()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT 1"));
        _sqlExecution.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(new QueryResult { ColumnNames = ["n"], Rows = [new Dictionary<string, object?> { ["n"] = 1 }] });
    }

    [Fact]
    public async Task SendMessageAsync_MultipleTurns_AccumulatesHistory()
    {
        await _sut.SendMessageAsync("q1");
        await _sut.SendMessageAsync("q2");
        await _sut.SendMessageAsync("q3");

        _sut.GetHistory().Should().HaveCount(3);
    }

    [Fact]
    public async Task SendMessageAsync_ExceedsMaxExchanges_OldestDropped()
    {
        // Send 11 messages to exceed the 10-exchange cap
        for (var i = 1; i <= 11; i++)
        {
            await _sut.SendMessageAsync($"question {i}");
        }

        _sut.GetHistory().Should().HaveCount(ConversationSession.MaxExchanges);
        _sut.GetHistory()[0].UserMessage.Should().Be("question 2");
    }

    [Fact]
    public async Task SendMessageAsync_AtExactMaxExchanges_DoesNotTrim()
    {
        for (var i = 1; i <= ConversationSession.MaxExchanges; i++)
        {
            await _sut.SendMessageAsync($"question {i}");
        }

        _sut.GetHistory().Should().HaveCount(ConversationSession.MaxExchanges);
        _sut.GetHistory()[0].UserMessage.Should().Be("question 1");
    }

    [Fact]
    public async Task SendMessageAsync_HistoryPassedToTextToSql()
    {
        // Capture a snapshot of the history each time GenerateSqlAsync is called
        var capturedHistories = new List<IReadOnlyList<ChatExchange>>();
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<ChatExchange>>(h => capturedHistories.Add(h.ToList())), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT 1"));

        await _sut.SendMessageAsync("q1");
        await _sut.SendMessageAsync("q2");

        capturedHistories.Should().HaveCount(2);
        // Second call should have history with q1
        capturedHistories[1].Should().HaveCount(1);
        capturedHistories[1][0].UserMessage.Should().Be("q1");
    }

    [Fact]
    public async Task Reset_AfterMultipleTurns_ClearsAllHistory()
    {
        await _sut.SendMessageAsync("q1");
        await _sut.SendMessageAsync("q2");

        _sut.Reset();

        _sut.GetHistory().Should().BeEmpty();
    }

    [Fact]
    public async Task SendMessageAsync_AfterReset_StartsWithEmptyHistory()
    {
        var capturedHistories = new List<IReadOnlyList<ChatExchange>>();
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Do<IReadOnlyList<ChatExchange>>(h => capturedHistories.Add(h.ToList())), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT 1"));

        await _sut.SendMessageAsync("q1");
        _sut.Reset();

        await _sut.SendMessageAsync("q2");

        _sut.GetHistory().Should().HaveCount(1);
        _sut.GetHistory()[0].UserMessage.Should().Be("q2");

        // History captured for q2 call should be empty (after reset)
        capturedHistories.Should().HaveCount(2);
        capturedHistories[1].Should().BeEmpty();
    }
}
