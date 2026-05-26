using FluentAssertions;
using NSubstitute;
using SalesChatbot.Models;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Services;

public class ConversationServiceSingleTurnTests
{
    private readonly ITextToSqlService _textToSql = Substitute.For<ITextToSqlService>();
    private readonly ISqlExecutionService _sqlExecution = Substitute.For<ISqlExecutionService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ConversationService _sut;

    public ConversationServiceSingleTurnTests()
    {
        _sut = new ConversationService(_textToSql, _sqlExecution, _auditService);
    }

    private static QueryResult EmptyResult() => new()
    {
        ColumnNames = [],
        Rows = []
    };

    private static QueryResult OneRowResult() => new()
    {
        ColumnNames = ["Count"],
        Rows = [new Dictionary<string, object?> { ["Count"] = 42 }]
    };

    [Fact]
    public async Task SendMessageAsync_ValidQuestion_ReturnsInterpreterAnswer()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT COUNT(*) FROM Orders"));
        _sqlExecution.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OneRowResult());
      

        var result = await _sut.SendMessageAsync("how many orders?");

        result.Should().Be("There are 42 orders.");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SendMessageAsync_EmptyOrWhitespace_ReturnsRephraseDeflection(string message)
    {
        var result = await _sut.SendMessageAsync(message);

        result.Should().Be(DeflectionMessages.Rephrase);
        await _textToSql.DidNotReceiveWithAnyArgs().GenerateSqlAsync(default!, default!, default);
    }

    [Fact]
    public async Task SendMessageAsync_CannotAnswerFromTextToSql_ReturnsOutOfScopeDeflection()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure(ChatConstants.CannotAnswer));

        var result = await _sut.SendMessageAsync("what is the weather today?");

        result.Should().Be(DeflectionMessages.OutOfScope);
        await _sqlExecution.DidNotReceiveWithAnyArgs().ExecuteQueryAsync(default!, default);
    }

    [Fact]
    public async Task SendMessageAsync_SqlValidationFailure_ReturnsDeflection()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure("Only SELECT statements are allowed."));

        var result = await _sut.SendMessageAsync("delete all orders");

        result.Should().NotBeNullOrWhiteSpace();
        await _sqlExecution.DidNotReceiveWithAnyArgs().ExecuteQueryAsync(default!, default);
    }

    [Fact]
    public async Task SendMessageAsync_SuccessfulTurn_AddsExchangeToHistory()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT 1"));
        _sqlExecution.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OneRowResult());
        
        await _sut.SendMessageAsync("question");

        _sut.GetHistory().Should().HaveCount(1);
        _sut.GetHistory()[0].UserMessage.Should().Be("question");
        _sut.GetHistory()[0].AssistantMessage.Should().Be("answer");
    }

    [Fact]
    public async Task SendMessageAsync_CannotAnswerTurn_AddsExchangeToHistory()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure(ChatConstants.CannotAnswer));

        await _sut.SendMessageAsync("out of scope question");

        _sut.GetHistory().Should().HaveCount(1);
    }

    [Fact]
    public void GetHistory_InitialState_ReturnsEmpty()
    {
        _sut.GetHistory().Should().BeEmpty();
    }

    [Fact]
    public async Task Reset_ClearsHistory()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT 1"));
        _sqlExecution.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(OneRowResult());
       
        await _sut.SendMessageAsync("question");
        _sut.Reset();

        _sut.GetHistory().Should().BeEmpty();
    }
}
