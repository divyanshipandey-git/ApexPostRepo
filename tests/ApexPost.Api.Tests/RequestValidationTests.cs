using System.ComponentModel.DataAnnotations;
using ApexPost.Api.DTOs;
using Xunit;

namespace ApexPost.Api.Tests;

/// <summary>
/// The DataAnnotations on the DTOs are the single source of truth for request validation
/// ([ApiController] runs them and answers 400 ValidationProblemDetails).
/// </summary>
public sealed class RequestValidationTests
{
    private static List<ValidationResult> Validate(object instance)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(instance, new ValidationContext(instance), results, validateAllProperties: true);
        return results;
    }

    private static CreateShipmentRequest Valid(
        string tracking = "APX-20241107", decimal weight = 2.5m, string phone = "+15551234567") => new()
    {
        TrackingNumber = tracking,
        WeightKg = weight,
        RecipientName = "John Doe",
        RecipientPhone = phone,
        RecipientAddress = "123 Main St",
        RecipientCity = "Springfield"
    };

    [Fact]
    public async Task ValidShipment_HasNoErrors()
    {
        Assert.Empty(Validate(Valid()));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task TrackingNumber_MustBeARealDate()
    {
        Assert.Single(Validate(Valid(tracking: "APX-12345678")));   // 8 digits but not a date
        Assert.Single(Validate(Valid(tracking: "APX-20241307")));   // month 13
        Assert.Single(Validate(Valid(tracking: "APX-2024110")));    // 7 digits
        Assert.Empty(Validate(Valid(tracking: "APX-20240229")));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Weight_IsBetweenPoint1And70()
    {
        Assert.Single(Validate(Valid(weight: 0m)));
        Assert.Single(Validate(Valid(weight: 0.05m)));
        Assert.Empty(Validate(Valid(weight: 0.1m)));
        Assert.Empty(Validate(Valid(weight: 70m)));
        Assert.Single(Validate(Valid(weight: 70.01m)));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task Phone_MustBeE164()
    {
        Assert.Single(Validate(Valid(phone: "555-1234")));
        Assert.Empty(Validate(Valid(phone: "+15551234567")));
        await Task.CompletedTask;
    }

    [Fact]
    public async Task UpdateStatus_DeliveredNeedsReasonAndSignature()
    {
        var request = new UpdateStatusRequest { Status = ShipmentStatus.Delivered };
        Assert.Equal(2, Validate(request).Count);

        var complete = new UpdateStatusRequest { Status = ShipmentStatus.Delivered, Reason = "Handed over", RecipientSignature = "J. Doe" };
        Assert.Empty(Validate(complete));
        await Task.CompletedTask;
    }
}
