using ApexPost.Domain.Entities;
using Xunit;

namespace ApexPost.UnitTests.Shipping;

/// <summary>
/// Risk R1: the status state machine of TrackedShipment, checked as a full 6 x 6 matrix.
/// </summary>
public sealed class ShipmentStatusTransitionTests
{
    private static readonly DateTimeOffset Created = new(2025, 3, 10, 8, 0, 0, TimeSpan.Zero);

    private static TrackedShipment NewShipment() => TrackedShipment.Create(
        "APX-20250101", 2.5m, "Jane Example", "+15550100", "1 Example Street", "Springfield", createdAtUtc: Created);

    /// <summary>Walks a new shipment along the legal path until it reaches <paramref name="target"/>.</summary>
    private static TrackedShipment ShipmentIn(ShipmentStatus target)
    {
        var shipment = NewShipment();
        switch (target)
        {
            case ShipmentStatus.Created:
                break;
            case ShipmentStatus.PickedUp:
                shipment.UpdateStatus(ShipmentStatus.PickedUp);
                break;
            case ShipmentStatus.InTransit:
                shipment.UpdateStatus(ShipmentStatus.PickedUp);
                shipment.UpdateStatus(ShipmentStatus.InTransit);
                break;
            case ShipmentStatus.OutForDelivery:
                shipment.UpdateStatus(ShipmentStatus.PickedUp);
                shipment.UpdateStatus(ShipmentStatus.InTransit);
                shipment.UpdateStatus(ShipmentStatus.OutForDelivery);
                break;
            case ShipmentStatus.Delivered:
                shipment.UpdateStatus(ShipmentStatus.PickedUp);
                shipment.UpdateStatus(ShipmentStatus.InTransit);
                shipment.UpdateStatus(ShipmentStatus.Delivered, recipientSignature: "Jane Example");
                break;
            case ShipmentStatus.Failed:
                shipment.UpdateStatus(ShipmentStatus.Failed, reason: "Address not found");
                break;
        }

        Assert.Equal(target, shipment.Status);
        return shipment;
    }

    private static readonly HashSet<(ShipmentStatus From, ShipmentStatus To)> Allowed =
    [
        (ShipmentStatus.Created, ShipmentStatus.PickedUp),
        (ShipmentStatus.Created, ShipmentStatus.Failed),
        (ShipmentStatus.PickedUp, ShipmentStatus.InTransit),
        (ShipmentStatus.PickedUp, ShipmentStatus.Failed),
        (ShipmentStatus.InTransit, ShipmentStatus.OutForDelivery),
        (ShipmentStatus.InTransit, ShipmentStatus.Delivered),
        (ShipmentStatus.InTransit, ShipmentStatus.Failed),
        (ShipmentStatus.OutForDelivery, ShipmentStatus.Delivered),
        (ShipmentStatus.OutForDelivery, ShipmentStatus.Failed),
    ];

    public static TheoryData<ShipmentStatus, ShipmentStatus, bool> Matrix()
    {
        var data = new TheoryData<ShipmentStatus, ShipmentStatus, bool>();
        foreach (var from in Enum.GetValues<ShipmentStatus>())
        {
            foreach (var to in Enum.GetValues<ShipmentStatus>())
            {
                data.Add(from, to, Allowed.Contains((from, to)));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(Matrix))]
    public void UpdateStatus_FromEveryStateToEveryState_FollowsTheTransitionTable(
        ShipmentStatus from, ShipmentStatus to, bool legal)
    {
        // Arrange
        var shipment = ShipmentIn(from);

        // Act
        var act = () => shipment.UpdateStatus(to, reason: "Because", recipientSignature: "Jane Example");

        // Assert
        if (legal)
        {
            act();
            Assert.Equal(to, shipment.Status);
        }
        else
        {
            var ex = Assert.Throws<InvalidOperationException>(act);
            Assert.Contains($"Invalid status transition from {from} to {to}", ex.Message);
            Assert.Equal(from, shipment.Status);
        }
    }

    [Fact]
    public void UpdateStatus_Delivered_CannotMoveBackToInTransit()
    {
        // Arrange
        var shipment = ShipmentIn(ShipmentStatus.Delivered);

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => shipment.UpdateStatus(ShipmentStatus.InTransit));
        Assert.Equal(ShipmentStatus.Delivered, shipment.Status);
    }

    [Fact]
    public void UpdateStatus_ToDeliveredWithoutSignature_ThrowsAndKeepsStatus()
    {
        // Arrange
        var shipment = ShipmentIn(ShipmentStatus.OutForDelivery);

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => shipment.UpdateStatus(ShipmentStatus.Delivered, recipientSignature: "  "));

        // Assert
        Assert.Contains("signature", ex.Message);
        Assert.Equal(ShipmentStatus.OutForDelivery, shipment.Status);
    }

    [Fact]
    public void UpdateStatus_ToFailedWithoutReason_ThrowsAndKeepsStatus()
    {
        // Arrange
        var shipment = ShipmentIn(ShipmentStatus.PickedUp);

        // Act + Assert
        Assert.Throws<InvalidOperationException>(() => shipment.UpdateStatus(ShipmentStatus.Failed, reason: null));
        Assert.Equal(ShipmentStatus.PickedUp, shipment.Status);
    }

    [Fact]
    public void UpdateStatus_EveryLegalChange_AppendsExactlyOneScanToHistory()
    {
        // Arrange
        var shipment = ShipmentIn(ShipmentStatus.PickedUp);
        var before = shipment.Scans.Count;

        // Act
        shipment.UpdateStatus(ShipmentStatus.InTransit, latitude: 40.1, longitude: -75.2);

        // Assert
        Assert.Equal(before + 1, shipment.Scans.Count);
        Assert.Equal("40.1,-75.2", shipment.Scans.Last().Location);
    }

    [Theory]
    [InlineData(ShipmentStatus.Created, true)]
    [InlineData(ShipmentStatus.PickedUp, false)]
    [InlineData(ShipmentStatus.Delivered, false)]
    public void UpdateDetails_OnlyAllowedWhileCreated(ShipmentStatus status, bool allowed)
    {
        // Arrange
        var shipment = ShipmentIn(status);

        // Act
        var act = () => shipment.UpdateDetails(recipientCity: "Shelbyville");

        // Assert
        if (allowed)
        {
            act();
            Assert.Equal("Shelbyville", shipment.RecipientCity);
        }
        else
        {
            Assert.Throws<InvalidOperationException>(act);
        }
    }
}
