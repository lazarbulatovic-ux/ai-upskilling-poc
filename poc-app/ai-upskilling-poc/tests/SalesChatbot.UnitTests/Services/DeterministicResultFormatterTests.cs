using FluentAssertions;
using SalesChatbot.Models;
using SalesChatbot.Services;

namespace SalesChatbot.UnitTests.Services;

public class DeterministicResultFormatterTests
{
    private readonly DeterministicResultFormatter _sut = new();

    // ── Format detection ────────────────────────────────────────────────────

    [Fact]
    public async Task InterpretAsync_ZeroRows_ReturnsNoResultsSentence()
    {
        var result = MakeResult([], []);

        var output = await _sut.InterpretAsync("any question", result, []);

        output.Should().Contain("No results");
    }

    [Fact]
    public async Task InterpretAsync_SingleValue_ReturnsOneSentence()
    {
        var result = MakeResult(["OrderCount"], [new Dictionary<string, object?> { ["OrderCount"] = 42 }]);

        var output = await _sut.InterpretAsync("how many?", result, []);

        output.Should().Contain("42");
        output.Should().NotContain("|"); // not a table
    }

    [Fact]
    public async Task InterpretAsync_MultipleRows_ReturnsMarkdownTable()
    {
        var rows = Enumerable.Range(1, 5)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Name"] = $"Customer {i}" })
            .ToList();
        var result = MakeResult(["Name"], rows);

        var output = await _sut.InterpretAsync("list customers", result, []);

        output.Should().StartWith("|");
        output.Should().Contain("| Name |");
    }

    // ── US5: Row cap removal ─────────────────────────────────────────────────

    [Fact]
    public async Task InterpretAsync_75Rows_ShowsAll75DataRows()
    {
        var rows = Enumerable.Range(1, 75)
            .Select(i => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?> { ["Id"] = i, ["Name"] = $"Item {i}" })
            .ToList();
        var result = MakeResult(["Id", "Name"], rows);

        var output = await _sut.InterpretAsync("list all", result, []);

        // Count data rows (lines starting with | that aren't the header or separator)
        var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var dataRows = lines.Count(l => l.TrimStart().StartsWith("|") && !l.Contains("---"));
        // dataRows includes the header line, so subtract 1
        (dataRows - 1).Should().Be(75, "all 75 rows must be present with no cap");
    }

    // ── US4: Grouped results ─────────────────────────────────────────────────

    [Fact]
    public async Task InterpretAsync_10GroupedRows_ShowsAllGroups()
    {
        var categories = new[] { "Electronics", "Office", "Accessories", "Software", "Hardware",
                                  "Furniture", "Lighting", "Cables", "Peripherals", "Storage" };
        var rows = categories
            .Select(c => (IReadOnlyDictionary<string, object?>)new Dictionary<string, object?>
            {
                ["Category"] = c,
                ["Revenue"] = (decimal)(1000 * Array.IndexOf(categories, c) + 500)
            })
            .ToList();
        var result = MakeResult(["Category", "Revenue"], rows);

        var output = await _sut.InterpretAsync("revenue by category", result, []);

        foreach (var cat in categories)
        {
            output.Should().Contain(cat, $"category '{cat}' must appear in the table");
        }
    }

    // ── Value formatting ─────────────────────────────────────────────────────

    [Fact]
    public void FormatValue_CurrencyColumn_FormatsWithEuroSign()
    {
        var formatted = DeterministicResultFormatter.FormatValue("TotalRevenue", 18450m);

        formatted.Should().Be("€18,450.00");
    }

    [Fact]
    public void FormatValue_NullValue_ReturnsEmpty()
    {
        var formatted = DeterministicResultFormatter.FormatValue("Name", null);

        formatted.Should().BeEmpty();
    }

    [Fact]
    public void FormatValue_DateTimeValue_FormatsReadably()
    {
        var dt = new DateTime(2026, 5, 18);

        var formatted = DeterministicResultFormatter.FormatValue("OrderDate", dt);

        formatted.Should().Be("18 May 2026");
    }

    [Fact]
    public void HumaniseHeader_PascalCase_InsertsSpaces()
    {
        var result = DeterministicResultFormatter.HumaniseHeader("TotalRevenue");

        result.Should().Be("Total Revenue");
    }

    [Fact]
    public void HumaniseHeader_OrderDate_Humanised()
    {
        var result = DeterministicResultFormatter.HumaniseHeader("OrderDate");

        result.Should().Be("Order Date");
    }

    [Fact]
    public void HumaniseHeader_CustomerCount_Humanised()
    {
        var result = DeterministicResultFormatter.HumaniseHeader("CustomerCount");

        result.Should().Be("Customer Count");
    }

    [Fact]
    public void FormatValue_PriceColumn_FormatsWithEuro()
    {
        var formatted = DeterministicResultFormatter.FormatValue("UnitPrice", 99.99m);

        formatted.Should().Be("€99.99");
    }

    [Fact]
    public void FormatValue_NonCurrencyDecimal_FormatsWithoutEuro()
    {
        var formatted = DeterministicResultFormatter.FormatValue("Quantity", 1234m);

        formatted.Should().Be("1,234.00");
        formatted.Should().NotContain("€");
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private static QueryResult MakeResult(
        IReadOnlyList<string> columns,
        IReadOnlyList<IReadOnlyDictionary<string, object?>> rows) =>
        new() { ColumnNames = columns, Rows = rows };
}
