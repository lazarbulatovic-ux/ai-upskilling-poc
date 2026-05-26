using FluentAssertions;
using SalesChatbot.Services.Validation;

namespace SalesChatbot.UnitTests.Validation;

public class SqlSafetyValidatorTests
{
    // ── IsValidSelect ────────────────────────────────────────────────────────

    [Fact]
    public void IsValidSelect_NullInput_ReturnsFalse()
    {
        var result = SqlSafetyValidator.IsValidSelect(null, out var reason);

        result.Should().BeFalse();
        reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void IsValidSelect_EmptyString_ReturnsFalse()
    {
        var result = SqlSafetyValidator.IsValidSelect("", out var reason);

        result.Should().BeFalse();
        reason.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void IsValidSelect_CannotAnswerSentinel_ReturnsFalse()
    {
        var result = SqlSafetyValidator.IsValidSelect("CANNOT_ANSWER", out var reason);

        result.Should().BeFalse();
        reason.Should().Contain("Sentinel");
    }

    [Fact]
    public void IsValidSelect_ValidSelect_ReturnsTrue()
    {
        var result = SqlSafetyValidator.IsValidSelect("SELECT Id, Name FROM Customers", out var reason);

        result.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void IsValidSelect_SelectWithWhitespace_ReturnsTrue()
    {
        var result = SqlSafetyValidator.IsValidSelect("  SELECT TOP 10 * FROM Orders  ", out var reason);

        result.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void IsValidSelect_MultipleStatements_ReturnsFalse()
    {
        var result = SqlSafetyValidator.IsValidSelect("SELECT 1; SELECT 2", out var reason);

        result.Should().BeFalse();
        reason.Should().Contain("Multiple statements");
    }

    [Fact]
    public void IsValidSelect_NonSelectStatement_ReturnsFalse()
    {
        var result = SqlSafetyValidator.IsValidSelect("UPDATE Customers SET Name = 'X'", out var reason);

        result.Should().BeFalse();
        reason.Should().Contain("Only SELECT");
    }

    [Theory]
    [InlineData("SELECT * FROM Customers WHERE 1=1; DROP TABLE Customers--")]
    [InlineData("SELECT * FROM Orders; DELETE FROM Orders")]
    public void IsValidSelect_StatementWithSemicolon_ReturnsFalse(string sql)
    {
        var result = SqlSafetyValidator.IsValidSelect(sql, out _);

        result.Should().BeFalse();
    }

    [Theory]
    [InlineData("SELECT * FROM Customers; INSERT INTO Customers VALUES(1,'x','y')", "INSERT")]
    [InlineData("SELECT 1 UPDATE Orders SET Status='X'", "UPDATE")]
    [InlineData("SELECT 1; DELETE FROM Orders", "DELETE")]
    public void IsValidSelect_ForbiddenTokens_ReturnsFalse(string sql, string token)
    {
        _ = token;
        var result = SqlSafetyValidator.IsValidSelect(sql, out var reason);

        result.Should().BeFalse();
    }

    [Fact]
    public void IsValidSelect_SelectWithBlockComment_ReturnsTrue()
    {
        const string sql = "/* get all orders */ SELECT * FROM Orders";

        var result = SqlSafetyValidator.IsValidSelect(sql, out var reason);

        result.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void IsValidSelect_SelectWithLineComment_ReturnsTrue()
    {
        const string sql = "SELECT * FROM Orders -- return all";

        var result = SqlSafetyValidator.IsValidSelect(sql, out var reason);

        result.Should().BeTrue();
        reason.Should().BeNull();
    }

    [Fact]
    public void IsValidSelect_ExecXpToken_ReturnsFalse()
    {
        // EXEC is a forbidden token; it is detected as a whole word
        var result = SqlSafetyValidator.IsValidSelect("SELECT EXEC xp_cmdshell('dir')", out _);

        result.Should().BeFalse();
    }

    // ── EnforceRowLimit ──────────────────────────────────────────────────────

    [Fact]
    public void EnforceRowLimit_NoTopClause_InjectsTop500()
    {
        var limited = SqlSafetyValidator.EnforceRowLimit("SELECT * FROM Orders");

        limited.Should().ContainEquivalentOf("TOP 500");
    }

    [Fact]
    public void EnforceRowLimit_TopBelowLimit_Unchanged()
    {
        var limited = SqlSafetyValidator.EnforceRowLimit("SELECT TOP 100 * FROM Orders");

        limited.Should().ContainEquivalentOf("TOP 100");
        limited.Should().NotContainEquivalentOf("TOP 500");
    }

    [Fact]
    public void EnforceRowLimit_TopExceedsLimit_CappedAt500()
    {
        var limited = SqlSafetyValidator.EnforceRowLimit("SELECT TOP 1000 * FROM Orders");

        limited.Should().ContainEquivalentOf("TOP 500");
        limited.Should().NotContainEquivalentOf("TOP 1000");
    }

    [Fact]
    public void EnforceRowLimit_SelectDistinctWithoutTop_InjectsTop500()
    {
        var limited = SqlSafetyValidator.EnforceRowLimit("SELECT DISTINCT Name FROM Customers");

        limited.Should().ContainEquivalentOf("TOP 500");
    }

    // ── ContainsBlockedKeyword ──────────────────────────────────────────────

    [Fact]
    public void ContainsBlockedKeyword_NullInput_ReturnsFalse()
    {
        var result = SqlSafetyValidator.ContainsBlockedKeyword(null, out var kw);

        result.Should().BeFalse();
        kw.Should().BeNull();
    }

    [Fact]
    public void ContainsBlockedKeyword_ValidSelect_ReturnsFalse()
    {
        var result = SqlSafetyValidator.ContainsBlockedKeyword("SELECT COUNT(*) FROM Orders", out var kw);

        result.Should().BeFalse();
        kw.Should().BeNull();
    }

    [Fact]
    public void ContainsBlockedKeyword_DropKeyword_ReturnsTrue()
    {
        var result = SqlSafetyValidator.ContainsBlockedKeyword("SELECT * FROM Orders; DROP TABLE Orders", out var kw);

        result.Should().BeTrue();
        kw.Should().Be("DROP");
    }

    [Fact]
    public void ContainsBlockedKeyword_EraseKeyword_ReturnsTrue()
    {
        var result = SqlSafetyValidator.ContainsBlockedKeyword("ERASE FROM Orders", out var kw);

        result.Should().BeTrue();
        kw.Should().Be("ERASE");
    }

    [Fact]
    public void ContainsBlockedKeyword_ExecutorWord_DoesNotMatchExec()
    {
        // "EXECUTOR" contains "EXEC" as a substring but not as a whole word
        var result = SqlSafetyValidator.ContainsBlockedKeyword("SELECT ExecutorName FROM Staff", out var kw);

        result.Should().BeFalse();
        kw.Should().BeNull();
    }

    [Theory]
    [InlineData("DELETE FROM Orders")]
    [InlineData("TRUNCATE TABLE Orders")]
    [InlineData("INSERT INTO Orders VALUES(1)")]
    [InlineData("UPDATE Orders SET Status='X'")]
    [InlineData("ALTER TABLE Orders ADD Col INT")]
    public void ContainsBlockedKeyword_MutatingStatements_ReturnTrue(string sql)
    {
        var result = SqlSafetyValidator.ContainsBlockedKeyword(sql, out var kw);

        result.Should().BeTrue();
        kw.Should().NotBeNullOrEmpty();
    }
}
