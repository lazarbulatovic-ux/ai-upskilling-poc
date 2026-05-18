namespace SalesChatbot.Infrastructure.Dial;

public sealed class DialOptions
{
    public const string SectionName = "Dial";

    public string Endpoint { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string Deployment { get; set; } = "gpt-4o";
}

public sealed class DialChatRequest
{
    public required IReadOnlyList<DialChatRequestMessage> Messages { get; init; }

    public double Temperature { get; init; }
}

public sealed class DialChatRequestMessage
{
    public required string Role { get; init; }

    public required string Content { get; init; }
}

public sealed class DialChatResponse
{
    public List<DialChatChoice>? Choices { get; set; }
}

public sealed class DialChatChoice
{
    public DialChatResponseMessage? Message { get; set; }
}

public sealed class DialChatResponseMessage
{
    public string? Content { get; set; }
}
