namespace SalesChatbot.Data.Entities;

public class Customer
{
    public int Id { get; set; }

    public required string Name { get; set; }

    public required string Country { get; set; }

    public ICollection<SalesOrder> Orders { get; set; } = [];
}
