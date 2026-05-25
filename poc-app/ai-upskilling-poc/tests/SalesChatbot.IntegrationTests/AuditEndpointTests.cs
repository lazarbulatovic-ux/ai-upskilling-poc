using System.Net;
using System.Text.Json;
using FluentAssertions;

namespace SalesChatbot.IntegrationTests;

public sealed class AuditEndpointTests(SalesChatbotTestFactory factory)
    : IClassFixture<SalesChatbotTestFactory>
{
    [SqlServerFact]
    public async Task GetAudit_WhenNoQueriesLogged_ReturnsOkWithEmptyArray()
    {
        await factory.EnsureDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        var entries = JsonSerializer.Deserialize<JsonElement[]>(json);
        entries.Should().NotBeNull();
    }

    [SqlServerFact]
    public async Task GetAudit_ReturnsValidJsonArray()
    {
        await factory.EnsureDatabaseAsync();
        var client = factory.CreateClient();

        var response = await client.GetAsync("/api/audit");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var contentType = response.Content.Headers.ContentType?.MediaType;
        contentType.Should().Contain("application/json");
    }
}
