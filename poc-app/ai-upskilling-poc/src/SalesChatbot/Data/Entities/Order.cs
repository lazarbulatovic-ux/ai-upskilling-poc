namespace SalesChatbot.Data.Entities;

public class SalesOrder
{
    public int Id { get; set; }

    public int CustomerId { get; set; }

    public DateTime OrderDate { get; set; }

    public required string Status { get; set; }

    public Customer Customer { get; set; } = null!;

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
