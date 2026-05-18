using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.IntegrationTests;

/// <summary>
/// Integration tests for multi-turn conversation flow.
/// IDialClient is stubbed; DB-dependent turns require LocalDB via [SqlServerFact].
/// </summary>
public class MultiTurnChatTests : IClassFixture<SalesChatbotTestFactory>
{
    private readonly SalesChatbotTestFactory _factory;

    public MultiTurnChatTests(SalesChatbotTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostNew_ReturnsNoContent_AndResetsSession()
    {
        var client = _factory.CreateClient();

        var resetResponse = await client.PostAsync("/api/chat/new", null);

        resetResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [Fact]
    public async Task SendAmbiguousFollowUpWithoutContext_ReturnsOkWithDeflection()
    {
        // Reset session first
        var client = _factory.CreateClient();
        await client.PostAsync("/api/chat/new", null);

        // Ambiguous follow-up ("and ..." prefix) with no prior history
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "and what about total revenue?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatResponseDto>();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
    }

    [SqlServerFact]
    public async Task ThreeTurnConversation_AllTurnsReturnOk()
    {
        await _factory.EnsureDatabaseAsync();

        // Configure IDialClient stub for alternating SQL / interpretation calls
        var callCount = 0;
        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var temp = callInfo.Arg<double>();
                callCount++;
                return temp == 0
                    ? Task.FromResult("SELECT COUNT(*) AS N FROM Orders")
                    : Task.FromResult($"Answer {callCount}.");
            });

        var client = _factory.CreateClient();

        // Reset first to start with empty history
        await client.PostAsync("/api/chat/new", null);

        // Turn 1
        var r1 = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "How many orders are there?" });
        r1.StatusCode.Should().Be(HttpStatusCode.OK);
        var b1 = await r1.Content.ReadFromJsonAsync<ChatResponseDto>();
        b1!.SessionExchangeCount.Should().Be(1);

        // Turn 2
        var r2 = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "How many completed orders?" });
        r2.StatusCode.Should().Be(HttpStatusCode.OK);
        var b2 = await r2.Content.ReadFromJsonAsync<ChatResponseDto>();
        b2!.SessionExchangeCount.Should().Be(2);

        // Turn 3
        var r3 = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "What is the total revenue?" });
        r3.StatusCode.Should().Be(HttpStatusCode.OK);
        var b3 = await r3.Content.ReadFromJsonAsync<ChatResponseDto>();
        b3!.SessionExchangeCount.Should().Be(3);
    }

    private sealed record ChatResponseDto(string Reply, int SessionExchangeCount);
}
