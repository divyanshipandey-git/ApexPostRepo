using ApexPost.Api.DTOs;
using ApexPost.Api.Exceptions;
using ApexPost.Api.Services;
using ApexPost.Application.Abstractions.Persistence;
using ApexPost.Application.Abstractions.Repositories;
using ApexPost.Domain.Entities;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using DtoStatus = ApexPost.Api.DTOs.ShipmentStatus;
using DomainStatus = ApexPost.Domain.Entities.ShipmentStatus;

namespace ApexPost.UnitTests.Api;

/// <summary>
/// ShipmentApiService with ITrackedShipmentRepository and IUnitOfWork mocked (risk R2: duplicate tracking numbers,
/// R1: status rules at service level). MockBehavior.Strict makes an unexpected call fail the test, so "nothing is saved"
/// is proven by the mock itself, not only by a Verify.
/// What these mocks do NOT prove: that the real unique index rejects a second row. The SQLite repository tests and the
/// API endpoint tests (409) cover that against a real database; real SQL Server error mapping is in the opt-in SQL tests.
/// </summary>
public sealed class ShipmentApiServiceMoqTests
{
    private const string Tracking = "APX-20250101";

    private readonly Mock<ITrackedShipmentRepository> _repository = new(MockBehavior.Strict);
    private readonly Mock<IUnitOfWork> _unitOfWork = new(MockBehavior.Strict);

    private ShipmentApiService CreateSut() =>
        new(_repository.Object, _unitOfWork.Object, NullLogger<ShipmentApiService>.Instance);

    private static CreateShipmentRequest Request(string tracking = Tracking, decimal weight = 2.5m) => new()
    {
        TrackingNumber = tracking,
        WeightKg = weight,
        RecipientName = "Jane Example",
        RecipientPhone = "+15550100",
        RecipientAddress = "1 Example Street",
        RecipientCity = "Springfield"
    };

    private static TrackedShipment Existing(DomainStatus? advanceTo = null)
    {
        var shipment = TrackedShipment.Create(Tracking, 2.5m, "Jane Example", "+15550100", "1 Example Street", "Springfield");
        if (advanceTo == DomainStatus.PickedUp)
        {
            shipment.UpdateStatus(DomainStatus.PickedUp);
        }

        return shipment;
    }

    private void GivenNoShipment() =>
        _repository.Setup(r => r.GetByTrackingNumberAsync(Tracking, It.IsAny<CancellationToken>()))
            .ReturnsAsync((TrackedShipment?)null);

    private void GivenShipment(TrackedShipment shipment) =>
        _repository.Setup(r => r.GetByTrackingNumberAsync(Tracking, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shipment);

    [Fact]
    public async Task CreateShipmentAsync_NewTrackingNumber_AddsOnceAndSavesOnce()
    {
        // Arrange
        GivenNoShipment();
        _repository.Setup(r => r.AddAsync(It.Is<TrackedShipment>(s => s.TrackingNumber == Tracking && s.Status == DomainStatus.Created),
            It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        var created = await CreateSut().CreateShipmentAsync(Request());

        // Assert
        Assert.Equal(Tracking, created.TrackingNumber);
        _repository.Verify(r => r.AddAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CreateShipmentAsync_DuplicateTrackingNumber_ThrowsConflictAndNeverAddsOrSaves()
    {
        // Arrange: strict mocks, so AddAsync or SaveChangesAsync would throw MockException if called.
        GivenShipment(Existing());

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().CreateShipmentAsync(Request()));
        _repository.Verify(r => r.AddAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>()), Times.Never);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CreateShipmentAsync_TwoRequestsRaceForTheSameNumber_UniqueKeyBecomesConflict()
    {
        // Arrange: the existence check passed, then the unique index rejected the insert.
        GivenNoShipment();
        _repository.Setup(r => r.AddAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UniqueKeyConflictException("duplicate", new Exception("inner")));

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().CreateShipmentAsync(Request()));
    }

    [Theory]
    [InlineData("2.5", "0")]
    [InlineData("40", "20.00")]
    public async Task CreateShipmentV2Async_ReturnsTheSurchargeOfTheNewShipment(string weight, string expected)
    {
        // Arrange
        GivenNoShipment();
        _repository.Setup(r => r.AddAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);
        var kg = decimal.Parse(weight, System.Globalization.CultureInfo.InvariantCulture);

        // Act
        var created = await CreateSut().CreateShipmentV2Async(Request(weight: kg));

        // Assert
        Assert.Equal(decimal.Parse(expected, System.Globalization.CultureInfo.InvariantCulture), created.SurchargeAmount);
    }

    [Fact]
    public async Task GetShipmentAsync_UnknownTrackingNumber_ThrowsNotFound()
    {
        // Arrange
        GivenNoShipment();

        // Act + Assert
        await Assert.ThrowsAsync<NotFoundException>(() => CreateSut().GetShipmentAsync(Tracking));
    }

    [Fact]
    public async Task UpdateShipmentStatusAsync_IllegalTransition_ThrowsConflictAndSavesNothing()
    {
        // Arrange: Created cannot go straight to Delivered.
        GivenShipment(Existing());

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().UpdateShipmentStatusAsync(
            Tracking, new UpdateStatusRequest { Status = DtoStatus.Delivered, Reason = "x", RecipientSignature = "Jane Example" }));
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task UpdateShipmentStatusAsync_LegalTransition_UpdatesAndSavesOnce()
    {
        // Arrange
        var shipment = Existing();
        GivenShipment(shipment);
        _repository.Setup(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await CreateSut().UpdateShipmentStatusAsync(Tracking, new UpdateStatusRequest { Status = DtoStatus.PickedUp });

        // Assert
        Assert.Equal(DomainStatus.PickedUp, shipment.Status);
        _repository.Verify(r => r.UpdateAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteShipmentAsync_AfterPickup_ThrowsConflictAndDeletesNothing()
    {
        // Arrange
        GivenShipment(Existing(DomainStatus.PickedUp));

        // Act + Assert
        await Assert.ThrowsAsync<ConflictException>(() => CreateSut().DeleteShipmentAsync(Tracking));
        _repository.Verify(r => r.DeleteAsync(It.IsAny<TrackedShipment>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteShipmentAsync_WhileCreated_DeletesOnceAndSavesOnce()
    {
        // Arrange
        var shipment = Existing();
        GivenShipment(shipment);
        _repository.Setup(r => r.DeleteAsync(shipment, It.IsAny<CancellationToken>())).Returns(Task.CompletedTask);
        _unitOfWork.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(1);

        // Act
        await CreateSut().DeleteShipmentAsync(Tracking);

        // Assert
        _repository.Verify(r => r.DeleteAsync(shipment, It.IsAny<CancellationToken>()), Times.Once);
        _unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }
}
