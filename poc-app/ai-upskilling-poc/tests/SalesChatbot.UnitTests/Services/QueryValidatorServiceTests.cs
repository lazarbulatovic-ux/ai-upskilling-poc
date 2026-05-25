using FluentAssertions;
using Microsoft.Extensions.Logging;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using SalesChatbot.Services;
using SalesChatbot.Services.Interfaces;

namespace SalesChatbot.UnitTests.Services;

public class QueryValidatorServiceTests
{
    private readonly IDialClient _dialClient = Substitute.For<IDialClient>();
    private readonly QueryValidatorService _sut;

    public QueryValidatorServiceTests()
    {
        _sut = new QueryValidatorService(_dialClient, Substitute.For<ILogger<QueryValidatorService>>());
    }

    [Fact]
    public async Task ValidateAsync_DialReturnsApproved_ReturnsApprovedResult()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("APPROVED");

        var result = await _sut.ValidateAsync("SELECT COUNT(*) FROM Orders");

        result.IsApproved.Should().BeTrue();
        result.RejectionReason.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_DialReturnsRejected_ReturnsRejectedWithReason()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("REJECTED: potential data exfiltration");

        var result = await _sut.ValidateAsync("SELECT * FROM sys.tables");

        result.IsApproved.Should().BeFalse();
        result.RejectionReason.Should().Be("potential data exfiltration");
    }

    [Fact]
    public async Task ValidateAsync_DialThrows_FailsClosedWithRejection()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new HttpRequestException("DIAL unavailable"));

        var result = await _sut.ValidateAsync("SELECT 1");

        result.IsApproved.Should().BeFalse();
        result.RejectionReason.Should().Contain("unavailable");
    }

    [Fact]
    public async Task ValidateAsync_DialReturnsUnexpectedResponse_ReturnsRejected()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("Sure, looks fine!");

        var result = await _sut.ValidateAsync("SELECT 1");

        result.IsApproved.Should().BeFalse();
        result.RejectionReason.Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task ValidateAsync_ApprovedWithTrailingWhitespace_IsApproved()
    {
        _dialClient.GetChatCompletionAsync(Arg.Any<IReadOnlyList<DialChatMessage>>(), Arg.Any<double>(), Arg.Any<CancellationToken>())
            .Returns("  APPROVED  ");

        var result = await _sut.ValidateAsync("SELECT 1");

        result.IsApproved.Should().BeTrue();
    }
}
