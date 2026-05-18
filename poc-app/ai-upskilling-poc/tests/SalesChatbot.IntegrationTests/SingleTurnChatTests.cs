using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.IntegrationTests;

/// <summary>
/// Integration tests for single-turn Q&A via the /api/chat/message endpoint.
/// IDialClient is stubbed; DB-dependent tests require LocalDB via [SqlServerFact].
/// </summary>
public class SingleTurnChatTests : IClassFixture<SalesChatbotTestFactory>
{
    private readonly SalesChatbotTestFactory _factory;

    public SingleTurnChatTests(SalesChatbotTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMessage_EmptyMessage_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/message", new { message = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMessage_WhitespaceMessage_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/message", new { message = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostNew_ReturnsNoContent()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsync("/api/chat/new", null);

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }

    [SqlServerFact]
    public async Task PostMessage_OrderCountQuestion_DialReturnsValidSql_ReturnsOkWithReply()
    {
        await _factory.EnsureDatabaseAsync();

        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                // First call = SQL generation (temp 0); second call = interpretation (temp 0.3)
                var temp = callInfo.Arg<double>();
                return temp == 0
                    ? Task.FromResult("SELECT COUNT(*) AS OrderCount FROM Orders")
                    : Task.FromResult("There are some orders in the database.");
            });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "How many orders are there?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponseDto>();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
    }

    [SqlServerFact]
    public async Task PostMessage_CustomerCountQuestion_DialReturnsValidSql_ReturnsOkWithReply()
    {
        await _factory.EnsureDatabaseAsync();

        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var temp = callInfo.Arg<double>();
                return temp == 0
                    ? Task.FromResult("SELECT COUNT(*) AS CustomerCount FROM Customers")
                    : Task.FromResult("There are some customers.");
            });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "How many customers do we have?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponseDto>();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
    }

    [SqlServerFact]
    public async Task PostMessage_ProductSummaryQuestion_DialReturnsValidSql_ReturnsOkWithReply()
    {
        await _factory.EnsureDatabaseAsync();

        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var temp = callInfo.Arg<double>();
                return temp == 0
                    ? Task.FromResult("SELECT TOP 500 Name, Category FROM Products")
                    : Task.FromResult("Here are the products we sell.");
            });

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "What products do we sell?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatMessageResponseDto>();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
    }

    private sealed record ChatMessageResponseDto(string Reply, int SessionExchangeCount);
}
