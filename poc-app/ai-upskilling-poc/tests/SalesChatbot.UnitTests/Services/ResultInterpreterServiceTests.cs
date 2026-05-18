using FluentAssertions;
using NSubstitute;
using SalesChatbot.Models;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Services;

public class ResultInterpreterServiceTests
{
    private readonly IDialClient _dialClient = Substitute.For<IDialClient>();
    private readonly ResultInterpreterService _sut;

    public ResultInterpreterServiceTests()
    {
        _sut = new ResultInterpreterService(_dialClient);
    }

    private static QueryResult EmptyResult() => new()
    {
        ColumnNames = ["Id"],
        Rows = []
    };

    private static QueryResult SingleRowResult(string col, object value) => new()
    {
        ColumnNames = [col],
        Rows = [new Dictionary<string, object?> { [col] = value }]
    };

    private static QueryResult MultiRowResult(int rowCount) => new()
    {
        ColumnNames = ["Id", "Name"],
        Rows = Enumerable.Range(1, rowCount)
            .Select(i => (IReadOnlyDictionary<string, object?>)
                new Dictionary<string, object?> { ["Id"] = i, ["Name"] = $"Item {i}" })
            .ToList()
    };

    [Fact]
    public async Task InterpretAsync_ZeroRows_CallsDialWithZeroRowsContext()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("No matching data was found.");

        var result = await _sut.InterpretAsync("show orders", EmptyResult(), [], default);

        result.Should().Be("No matching data was found.");
        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Is<IReadOnlyList<DialChatMessage>>(msgs =>
                msgs.Any(m => m.Content.Contains("zero rows"))),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InterpretAsync_SingleRow_CallsDialWithData()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("There are 42 orders.");

        var result = await _sut.InterpretAsync("how many orders?", SingleRowResult("Count", 42), [], default);

        result.Should().Be("There are 42 orders.");
        await _dialClient.Received(1).GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InterpretAsync_UsesTempPoint3()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("answer");

        await _sut.InterpretAsync("q", SingleRowResult("n", 1), [], default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Any<IReadOnlyList<DialChatMessage>>(),
            0.3,
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InterpretAsync_WithHistory_PassesHistoryMessages()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("answer");

        var history = new List<ChatExchange>
        {
            new() { UserMessage = "prev question", AssistantMessage = "prev answer" }
        };

        await _sut.InterpretAsync("follow up", SingleRowResult("n", 5), history, default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Is<IReadOnlyList<DialChatMessage>>(msgs =>
                msgs.Any(m => m.Role == "user" && m.Content == "prev question") &&
                msgs.Any(m => m.Role == "assistant" && m.Content == "prev answer")),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InterpretAsync_MultiRowResult_PassesRowCountAndSampleRows()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("summary");

        await _sut.InterpretAsync("show products", MultiRowResult(10), [], default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Is<IReadOnlyList<DialChatMessage>>(msgs =>
                msgs.Any(m => m.Content.Contains("Row count: 10"))),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InterpretAsync_IncludesSystemPrompt()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("answer");

        await _sut.InterpretAsync("q", SingleRowResult("n", 1), [], default);

        await _dialClient.Received(1).GetChatCompletionAsync(
            Arg.Is<IReadOnlyList<DialChatMessage>>(msgs =>
                msgs.Any(m => m.Role == "system")),
            Arg.Any<double>(),
            Arg.Any<CancellationToken>());
    }
}
