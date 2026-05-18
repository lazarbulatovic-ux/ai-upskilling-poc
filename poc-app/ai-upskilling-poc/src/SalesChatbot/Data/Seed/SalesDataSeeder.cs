using Microsoft.EntityFrameworkCore;
using SalesChatbot.Data.Entities;

namespace SalesChatbot.Data.Seed;

public static class SalesDataSeeder
{
    public static async Task SeedAsync(SalesDbContext context, CancellationToken cancellationToken = default)
    {
        if (await context.Customers.AnyAsync(cancellationToken))
        {
            return;
        }

        var customers = new List<Customer>
        {
            new() { Name = "Acme GmbH", Country = "Germany" },
            new() { Name = "Berlin Retail", Country = "Germany" },
            new() { Name = "Paris Boutique", Country = "France" },
            new() { Name = "Milan Style", Country = "Italy" },
            new() { Name = "Nordic Supply", Country = "Sweden" }
        };

        context.Customers.AddRange(customers);
        await context.SaveChangesAsync(cancellationToken);

        var products = new List<Product>
        {
            new() { Name = "Widget A", Category = "Electronics" },
            new() { Name = "Widget B", Category = "Electronics" },
            new() { Name = "Widget C", Category = "Electronics" },
            new() { Name = "Desk Lamp", Category = "Furniture" },
            new() { Name = "Office Chair", Category = "Furniture" },
            new() { Name = "Notebook Pro", Category = "Electronics" },
            new() { Name = "USB Hub", Category = "Electronics" },
            new() { Name = "Standing Desk", Category = "Furniture" }
        };

        context.Products.AddRange(products);
        await context.SaveChangesAsync(cancellationToken);

        var random = new Random(42);
        var today = DateTime.UtcNow.Date;
        var orders = new List<SalesOrder>();
        var orderItems = new List<OrderItem>();

        for (var dayOffset = 0; dayOffset < 60; dayOffset++)
        {
            var ordersForDay = dayOffset <= 30 ? random.Next(3, 6) : random.Next(1, 4);
            for (var i = 0; i < ordersForDay; i++)
            {
                var customer = customers[random.Next(customers.Count)];
                var status = dayOffset % 5 == 0 ? "Pending"
                    : dayOffset % 7 == 0 ? "Cancelled"
                    : "Completed";

                var order = new SalesOrder
                {
                    CustomerId = customer.Id,
                    OrderDate = today.AddDays(-dayOffset).AddHours(random.Next(8, 18)),
                    Status = status
                };
                orders.Add(order);
            }
        }

        context.Orders.AddRange(orders);
        await context.SaveChangesAsync(cancellationToken);

        foreach (var order in orders)
        {
            var product = products[random.Next(products.Count)];
            orderItems.Add(new OrderItem
            {
                OrderId = order.Id,
                ProductId = product.Id,
                Quantity = random.Next(1, 5),
                UnitPrice = Math.Round((decimal)(random.NextDouble() * 200 + 20), 2)
            });
        }

        context.OrderItems.AddRange(orderItems);
        await context.SaveChangesAsync(cancellationToken);
    }
}
