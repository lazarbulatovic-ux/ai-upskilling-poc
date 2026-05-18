using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;
using SalesChatbot.Data;
using SalesChatbot.Data.Seed;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.IntegrationTests;

/// <summary>
/// WebApplicationFactory that replaces IDialClient with a configurable stub.
/// This allows integration tests to exercise the full pipeline without a live LLM endpoint.
/// If LocalDB is available, the database is migrated and seeded once during factory creation.
/// </summary>
public sealed class SalesChatbotTestFactory : WebApplicationFactory<Program>
{
    public IDialClient DialClient { get; } = Substitute.For<IDialClient>();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Use Development so the developer exception page shows actual errors in tests.
        // The migration block in Program.cs is guarded by IsDevelopment(); we let it run.
        builder.UseEnvironment("Development");

        builder.ConfigureServices(services =>
        {
            // Replace the typed-HttpClient IDialClient with a singleton NSubstitute stub
            var descriptors = services
                .Where(d => d.ServiceType == typeof(IDialClient))
                .ToList();
            foreach (var d in descriptors)
            {
                services.Remove(d);
            }

            services.AddSingleton<IDialClient>(DialClient);
        });
    }

    /// <summary>
    /// Migrate and seed the database if SQL Server is reachable.
    /// Safe to call multiple times (idempotent).
    /// </summary>
    public async Task EnsureDatabaseAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<SalesDbContext>();
        try
        {
            await db.Database.MigrateAsync();
            await SalesDataSeeder.SeedAsync(db);
        }
        catch
        {
            // If DB is unavailable the caller's [SqlServerFact] will skip the test.
        }
    }
}
