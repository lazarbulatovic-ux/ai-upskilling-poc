using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using SalesChatbot.Models;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Services;

public class TextToSqlServiceTests
{
    private readonly IDialClient _dialClient = Substitute.For<IDialClient>();
    private readonly IQueryValidatorService _queryValidator = Substitute.For<IQueryValidatorService>();
    private readonly TextToSqlService _sut;

    public TextToSqlServiceTests()
    {
        _queryValidator.ValidateAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(SqlValidationResult.Approved());
        _sut = new TextToSqlService(_dialClient, _queryValidator, Substitute.For<ILogger<TextToSqlService>>());
    }

    [Fact]
    public async Task GenerateSqlAsync_ValidSelectReturned_ReturnsSuccess()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("SELECT * FROM Orders");

        var result = await _sut.GenerateSqlAsync("show all orders", [], default);

        result.IsSuccess.Should().BeTrue();
        result.Sql.Should().Be("SELECT * FROM Orders");
    }

    [Fact]
    public async Task GenerateSqlAsync_CannotAnswerReturned_ReturnsFailure()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(ChatConstants.CannotAnswer);

        var result = await _sut.GenerateSqlAsync("what is the weather?", [], default);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().Be(ChatConstants.CannotAnswer);
    }

    [Fact]
    public async Task GenerateSqlAsync_NonSelectReturned_ReturnsFailure()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("DELETE FROM Orders");

        var result = await _sut.GenerateSqlAsync("delete all orders", [], default);

        result.IsSuccess.Should().BeFalse();
        result.FailureReason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task GenerateSqlAsync_ForbiddenTokenInSelect_ReturnsFailure()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("SELECT * FROM Orders; DROP TABLE Orders");

        var result = await _sut.GenerateSqlAsync("drop orders", [], default);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task GenerateSqlAsync_UsesTempZeroForSqlGeneration()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("SELECT 1");

        await _sut.GenerateSqlAsync("test", [], default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Any<IReadOnlyList<DialChatMessage>>(),
            0,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSqlAsync_WithHistory_IncludesHistoryMessages()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("SELECT COUNT(*) FROM Orders");

        var history = new List<ChatExchange>
        {
            new() { UserMessage = "how many customers?", AssistantMessage = "There are 5 customers." }
        };

        await _sut.GenerateSqlAsync("and how many orders?", history, default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Is<IReadOnlyList<DialChatMessage>>(msgs =>
                msgs.Any(m => m.Role == "user" && m.Content == "how many customers?") &&
                msgs.Any(m => m.Role == "assistant" && m.Content == "There are 5 customers.")),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSqlAsync_IncludesSystemPrompt()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("SELECT 1");

        await _sut.GenerateSqlAsync("test", [], default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Is<IReadOnlyList<DialChatMessage>>(msgs =>
                msgs.Any(m => m.Role == "system")),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GenerateSqlAsync_PropagatesCancellationToken()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns<string>(ci => throw new OperationCanceledException(ci.Arg<CancellationToken>()));

        var act = () => _sut.GenerateSqlAsync("q", [], cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
