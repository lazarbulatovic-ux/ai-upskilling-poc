namespace SalesChatbot.Services;

public static class ChatConstants
{
    public const string CannotAnswer = "CANNOT_ANSWER";
}

public static class DeflectionMessages
{
    public const string OutOfScope =
        "I can only answer questions about sales data. Please ask about orders, customers, or products.";

    public const string ReadOnly =
        "I can only answer read-only questions about sales data. Creating, updating, or deleting data is not supported.";

    public const string Rephrase =
        "Please rephrase your question about orders, customers, products, or sales metrics.";

    public const string MissingContext =
        "I need more context to answer that follow-up. Please ask a complete question or provide the details you are referring to.";

    public const string DataUnavailable =
        "Sales data is temporarily unavailable. Please try again later.";
}
