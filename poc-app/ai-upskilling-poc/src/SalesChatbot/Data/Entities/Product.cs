namespace SalesChatbot.Data.Entities;

public class Product
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Category { get; set; }

    public ICollection<OrderItem> OrderItems { get; set; } = [];
}
