using System.Reflection;
using ApexPost.Api.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Xunit;

namespace ApexPost.Api.Tests;

/// <summary>
/// Guards the authorization contract of the shipment endpoints: writes that create or replace shipments are
/// Admin-only, so a signed-in Courier (class-level [Authorize] only) can no longer reach them.
/// </summary>
public sealed class ShipmentAuthorizationTests
{
    [Theory]
    [InlineData(nameof(ShipmentsController.CreateShipmentV1))]
    [InlineData(nameof(ShipmentsController.CreateShipmentV2))]
    [InlineData(nameof(ShipmentsController.UpdateShipmentV1))]
    [InlineData(nameof(ShipmentsController.UpdateShipmentV2))]
    [InlineData(nameof(ShipmentsController.DeleteShipment))]
    public void ShipmentWrites_RequireAdminOnlyPolicy(string action)
    {
        var method = typeof(ShipmentsController).GetMethod(action)!;

        var policies = method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy);

        Assert.Contains("AdminOnly", policies);
    }

    [Fact]
    public void StatusChange_RemainsCourierOnly()
    {
        var method = typeof(ShipmentsController).GetMethod(nameof(ShipmentsController.UpdateShipmentStatus))!;

        Assert.Contains("CourierOnly", method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy));
    }

    [Fact]
    public void ShipmentReads_AreNotAdminOnly()
    {
        foreach (var action in new[] { nameof(ShipmentsController.GetShipmentV1), nameof(ShipmentsController.GetShipmentV2) })
        {
            var method = typeof(ShipmentsController).GetMethod(action)!;
            Assert.DoesNotContain("AdminOnly", method.GetCustomAttributes<AuthorizeAttribute>().Select(a => a.Policy));
        }
    }
}
