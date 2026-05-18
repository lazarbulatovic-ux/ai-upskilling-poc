using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using NSubstitute;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.IntegrationTests;

/// <summary>
/// Integration tests verifying that out-of-scope and write-request prompts are deflected
/// without fabricating sales data or exposing SQL.
/// IDialClient is stubbed to return CANNOT_ANSWER for off-topic queries.
/// </summary>
public class DeflectionChatTests : IClassFixture<SalesChatbotTestFactory>
{
    private readonly SalesChatbotTestFactory _factory;

    public DeflectionChatTests(SalesChatbotTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task PostMessage_EmptyString_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/message", new { message = "" });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMessage_WhitespaceOnly_ReturnsBadRequest()
    {
        var client = _factory.CreateClient();

        var response = await client.PostAsJsonAsync("/api/chat/message", new { message = "   " });

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task PostMessage_WeatherQuestion_DialReturnsCannotAnswer_ReturnsDeflection()
    {
        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(ChatConstants.CannotAnswer);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "What is the weather in Berlin today?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatResponseDto>();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
        body.Reply.Should().Be(DeflectionMessages.OutOfScope);
        body.Reply.Should().NotContain("SELECT");
        body.Reply.Should().NotContain("FROM");
    }

    [Fact]
    public async Task PostMessage_PayrollQuestion_DialReturnsCannotAnswer_ReturnsDeflection()
    {
        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns(ChatConstants.CannotAnswer);

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "What is the total payroll for January?" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatResponseDto>();
        body!.Reply.Should().Be(DeflectionMessages.OutOfScope);
        body.Reply.Should().NotContain("SELECT");
    }

    [Fact]
    public async Task PostMessage_DeleteRequestBypassingLLM_SqlValidatorCatches_ReturnsDeflection()
    {
        // Simulate a misbehaving LLM that returns DML — SqlSafetyValidator should catch it
        _factory.DialClient
            .GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("DELETE FROM Orders WHERE 1=1");

        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync("/api/chat/message",
            new { message = "Delete all orders from the database" });

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ChatResponseDto>();
        body!.Reply.Should().NotBeNullOrWhiteSpace();
        body.Reply.Should().NotContain("DELETE");
        body.Reply.Should().NotContain("FROM");
    }

    private sealed record ChatResponseDto(string Reply, int SessionExchangeCount);
}
