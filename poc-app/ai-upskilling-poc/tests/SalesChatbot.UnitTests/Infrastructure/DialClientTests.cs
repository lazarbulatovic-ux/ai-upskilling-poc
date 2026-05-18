using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.Extensions.Options;
using NSubstitute;
using SalesChatbot.Infrastructure.Dial;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Infrastructure;

public class DialClientTests
{
    private static DialOptions DefaultOptions => new()
    {
        Endpoint = "https://dial.example.com",
        ApiKey = "test-key",
        Deployment = "gpt-4o"
    };

    private static (DialClient client, FakeHandler handler) BuildClient(HttpResponseMessage response)
    {
        var handler = new FakeHandler(response);
        var httpClient = new HttpClient(handler);
        var options = Options.Create(DefaultOptions);
        var client = new DialClient(httpClient, options);
        return (client, handler);
    }

    private static HttpResponseMessage OkResponse(string content)
    {
        var payload = new
        {
            choices = new[]
            {
                new { message = new { content } }
            }
        };

        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(payload, options: new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            })
        };
    }

    [Fact]
    public async Task GetChatCompletionAsync_ValidResponse_ReturnsContent()
    {
        var (client, _) = BuildClient(OkResponse("SELECT * FROM Orders"));

        var result = await client.GetChatCompletionAsync(
            [new DialChatMessage("user", "show orders")], 0);

        result.Should().Be("SELECT * FROM Orders");
    }

    [Fact]
    public async Task GetChatCompletionAsync_TrimsWhitespaceFromContent()
    {
        var (client, _) = BuildClient(OkResponse("  SELECT 1  "));

        var result = await client.GetChatCompletionAsync(
            [new DialChatMessage("user", "test")], 0);

        result.Should().Be("SELECT 1");
    }

    [Fact]
    public async Task GetChatCompletionAsync_NonSuccessResponse_Throws()
    {
        var (client, _) = BuildClient(new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var act = () => client.GetChatCompletionAsync(
            [new DialChatMessage("user", "test")], 0);

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [Fact]
    public async Task GetChatCompletionAsync_CancellationRequested_ThrowsOperationCanceledException()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var (client, _) = BuildClient(OkResponse("SELECT 1"));

        var act = () => client.GetChatCompletionAsync(
            [new DialChatMessage("user", "test")], 0, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task GetChatCompletionAsync_SendsAuthorizationHeader()
    {
        var (client, handler) = BuildClient(OkResponse("SELECT 1"));

        await client.GetChatCompletionAsync([new DialChatMessage("user", "q")], 0);

        handler.LastRequest!.Headers.Authorization!.Scheme.Should().Be("Bearer");
        handler.LastRequest.Headers.Authorization.Parameter.Should().Be("test-key");
    }

    [Fact]
    public async Task GetChatCompletionAsync_UsesCorrectUrl()
    {
        var (client, handler) = BuildClient(OkResponse("SELECT 1"));

        await client.GetChatCompletionAsync([new DialChatMessage("user", "q")], 0);

        handler.LastRequest!.RequestUri!.ToString()
            .Should().Be("https://dial.example.com/openai/deployments/gpt-4o/chat/completions");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private sealed class FakeHandler(HttpResponseMessage response) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            LastRequest = request;
            return Task.FromResult(response);
        }
    }
}
