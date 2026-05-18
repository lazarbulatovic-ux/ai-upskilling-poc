using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.Api;

public static class ChatEndpoints
{
    public static IEndpointRouteBuilder MapChatEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/chat");

        group.MapPost("/message", async (
            ChatMessageRequest request,
            IConversationService conversationService,
            CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return Results.BadRequest(new ErrorResponse("Message is required."));
            }

            try
            {
                var reply = await conversationService.SendMessageAsync(request.Message, cancellationToken);
                return Results.Ok(new ChatMessageResponse(reply, conversationService.GetHistory().Count));
            }
            catch (Exception ex) when (IsDatabaseUnavailable(ex))
            {
                return Results.Json(
                    new ErrorResponse(DeflectionMessages.DataUnavailable),
                    statusCode: StatusCodes.Status503ServiceUnavailable);
            }
        });

        group.MapPost("/new", (IConversationService conversationService) =>
        {
            conversationService.Reset();
            return Results.NoContent();
        });

        return app;
    }

    private static bool IsDatabaseUnavailable(Exception ex) =>
        ex.Message.Contains("connection", StringComparison.OrdinalIgnoreCase)
        || ex.Message.Contains("network", StringComparison.OrdinalIgnoreCase);

    public sealed record ChatMessageRequest(string Message);

    public sealed record ChatMessageResponse(string Reply, int SessionExchangeCount);

    public sealed record ErrorResponse(string Error);
}
