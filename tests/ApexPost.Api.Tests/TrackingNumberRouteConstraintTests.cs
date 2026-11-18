using ApexPost.Api.Routing;
using Microsoft.AspNetCore.Routing;
using Xunit;

namespace ApexPost.Api.Tests;

public sealed class TrackingNumberRouteConstraintTests
{
    private static bool Matches(object? value)
    {
        var constraint = new TrackingNumberRouteConstraint();
        var values = new RouteValueDictionary { ["trackingNumber"] = value };
        return constraint.Match(httpContext: null, route: null, "trackingNumber", values, RouteDirection.IncomingRequest);
    }

    [Theory]
    [InlineData("APX-12345678")]
    [InlineData("APX-00000000")]
    [InlineData("APX-20240229")]
    public void Match_WhenFormatIsValid_ReturnsTrue(string tracking)
    {
        // Act
        var result = Matches(tracking);

        // Assert
        Assert.True(result);
    }

    [Theory]
    [InlineData("APX-1234567")]
    [InlineData("APX-123456789")]
    [InlineData("APX-ABCDEFGH")]
    [InlineData("XYZ-12345678")]
    [InlineData("apx-12345678")]
    [InlineData("")]
    [InlineData("APX-12345678\n")]
    [InlineData("APX-12345678\r\n")]
    [InlineData("APX-12345678 ")]
    [InlineData(" APX-12345678")]
    public void Match_WhenFormatIsInvalid_ReturnsFalse(string tracking)
    {
        // Act
        var result = Matches(tracking);

        // Assert: the newline cases matter because a plain "$" in .NET also matches before a final "\n".
        Assert.False(result);
    }

    [Fact]
    public void Match_WhenValueIsMissing_ReturnsFalse()
    {
        // Act
        var result = Matches(null);

        // Assert
        Assert.False(result);
    }
}
