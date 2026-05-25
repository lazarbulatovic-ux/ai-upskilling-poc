using FluentAssertions;
using NSubstitute;
using SalesChatbot.Models;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Services;

public class ConversationServiceDeflectionTests
{
    private readonly ITextToSqlService _textToSql = Substitute.For<ITextToSqlService>();
    private readonly ISqlExecutionService _sqlExecution = Substitute.For<ISqlExecutionService>();
    private readonly IResultInterpreterService _interpreter = Substitute.For<IResultInterpreterService>();
    private readonly IAuditService _auditService = Substitute.For<IAuditService>();
    private readonly ConversationService _sut;

    public ConversationServiceDeflectionTests()
    {
        _sut = new ConversationService(_textToSql, _sqlExecution, _interpreter, _auditService);
    }

    [Fact]
    public async Task SendMessageAsync_OutOfScopeQuestion_ReturnsOutOfScopeDeflection()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure(ChatConstants.CannotAnswer));

        var result = await _sut.SendMessageAsync("What is the weather in Paris?");

        result.Should().Be(DeflectionMessages.OutOfScope);
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("\t")]
    public async Task SendMessageAsync_EmptyInput_ReturnsRephraseDeflection(string input)
    {
        var result = await _sut.SendMessageAsync(input);

        result.Should().Be(DeflectionMessages.Rephrase);
    }

    [Fact]
    public async Task SendMessageAsync_WriteRequestCapturedByTextToSql_ReturnsReadOnlyDeflection()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure("Forbidden token 'DELETE' detected."));

        var result = await _sut.SendMessageAsync("delete all orders");

        result.Should().Be(DeflectionMessages.ReadOnly);
    }

    [Fact]
    public async Task SendMessageAsync_UpdateRequestCapturedByTextToSql_ReturnsReadOnlyDeflection()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure("Forbidden token 'UPDATE' detected."));

        var result = await _sut.SendMessageAsync("update customer name");

        result.Should().Be(DeflectionMessages.ReadOnly);
    }

    [Fact]
    public async Task SendMessageAsync_SqlExecutionThrows_ReturnsDeflection()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Success("SELECT 1"));
        _sqlExecution.ExecuteQueryAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<QueryResult>(_ => throw new InvalidOperationException("Only SELECT statements are allowed."));

        var result = await _sut.SendMessageAsync("some question");

        result.Should().Be(DeflectionMessages.OutOfScope);
    }

    [Fact]
    public async Task SendMessageAsync_AmbiguousFollowUpWithNoHistory_ReturnsMissingContextDeflection()
    {
        // "and " prefix with no history triggers ambiguous follow-up
        var result = await _sut.SendMessageAsync("and what about total revenue?");

        result.Should().Be(DeflectionMessages.MissingContext);
        await _textToSql.DidNotReceiveWithAnyArgs().GenerateSqlAsync(default!, default!, default);
    }

    [Fact]
    public async Task SendMessageAsync_WriteRequestBypassesTextToSql_SqlValidatorCatchesIt()
    {
        // Simulate TextToSql returning a write statement that was not caught by the LLM
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure("write"));

        var result = await _sut.SendMessageAsync("delete all products");

        result.Should().Be(DeflectionMessages.ReadOnly);
    }

    [Fact]
    public async Task SendMessageAsync_OutOfScopeDeflection_DoesNotExposeSQL()
    {
        _textToSql.GenerateSqlAsync(Arg.Any<string>(), Arg.Any<IReadOnlyList<ChatExchange>>(), Arg.Any<CancellationToken>())
            .Returns(SqlGenerationResult.Failure(ChatConstants.CannotAnswer));

        var result = await _sut.SendMessageAsync("what is payroll?");

        result.Should().NotContain("SELECT");
        result.Should().NotContain("FROM");
    }
}
