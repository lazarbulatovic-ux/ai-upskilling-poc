//using System.Net.Http.Headers;
//using System.Net.Http.Json;
//using System.Text.Json;
//using Microsoft.Extensions.Options;
//using SalesChatbot.Services.Interfaces;

//namespace SalesChatbot.Infrastructure.Dial;

//public sealed class DialClient(HttpClient httpClient, IOptions<DialOptions> options) : IDialClient
//{
//    private static readonly JsonSerializerOptions JsonOptions = new()
//    {
//        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
//    };

//    private readonly DialOptions _options = options.Value;

//    public async Task<string> GetChatCompletionAsync(
//        IReadOnlyList<DialChatMessage> messages,
//        double temperature,
//        CancellationToken cancellationToken = default)
//    {
//        var endpoint = _options.Endpoint.TrimEnd('/');
//        var url = $"{endpoint}/openai/deployments/{_options.Deployment}/chat/completions";

//        using var request = new HttpRequestMessage(HttpMethod.Post, url);
//        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
//        request.Content = JsonContent.Create(new DialChatRequest
//        {
//            Temperature = temperature,
//            Messages = messages.Select(m => new DialChatRequestMessage
//            {
//                Role = m.Role,
//                Content = m.Content
//            }).ToList()
//        }, options: JsonOptions);

//        using var response = await httpClient.SendAsync(request, cancellationToken);
//        response.EnsureSuccessStatusCode();

//        var payload = await response.Content.ReadFromJsonAsync<DialChatResponse>(JsonOptions, cancellationToken);
//        return payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
//               ?? throw new InvalidOperationException("DIAL returned an empty response.");
//    }
//}
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Infrastructure.Dial;

public sealed class DialClient(HttpClient httpClient, IOptions<DialOptions> options) : IDialClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly DialOptions _options = options.Value;

    public async Task<string> GetChatCompletionAsync(
        IReadOnlyList<DialChatMessage> messages,
        double temperature,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, _options.Endpoint);
        request.Headers.Add("api-key", _options.ApiKey);
        request.Content = JsonContent.Create(new DialChatRequest
        {
            Temperature = temperature,
            Messages = messages.Select(m => new DialChatRequestMessage
            {
                Role = m.Role,
                Content = m.Content
            }).ToList()
        }, options: JsonOptions);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<DialChatResponse>(JsonOptions, cancellationToken);
        return payload?.Choices?.FirstOrDefault()?.Message?.Content?.Trim()
               ?? throw new InvalidOperationException("DIAL returned an empty response.");
    }
}
