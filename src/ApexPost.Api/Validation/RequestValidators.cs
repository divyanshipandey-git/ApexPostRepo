using FluentValidation;
using ApexPost.Api.DTOs;

namespace ApexPost.Api.Validation;

/// <summary>
/// Validator for CreateShipmentRequest.
/// </summary>
public sealed class CreateShipmentRequestValidator : AbstractValidator<CreateShipmentRequest>
{
    public CreateShipmentRequestValidator()
    {
        RuleFor(x => x.TrackingNumber)
            .NotEmpty().WithMessage("Tracking number is required")
            .Matches("^APX-\\d{8}$").WithMessage("Tracking number must match format APX-YYYYMMDD");

        RuleFor(x => x.WeightKg)
            .GreaterThan(0).WithMessage("Weight must be greater than 0")
            .LessThanOrEqualTo(70).WithMessage("Weight must not exceed 70 kg");

        RuleFor(x => x.RecipientName)
            .NotEmpty().WithMessage("Recipient name is required")
            .MaximumLength(100).WithMessage("Recipient name must not exceed 100 characters");

        RuleFor(x => x.RecipientPhone)
            .NotEmpty().WithMessage("Recipient phone is required")
            .Matches(@"^\+?[1-9]\d{1,14}$").WithMessage("Phone number must be in valid E.164 format");

        RuleFor(x => x.RecipientAddress)
            .NotEmpty().WithMessage("Recipient address is required")
            .MaximumLength(200).WithMessage("Recipient address must not exceed 200 characters");

        RuleFor(x => x.RecipientCity)
            .NotEmpty().WithMessage("Recipient city is required")
            .MaximumLength(50).WithMessage("Recipient city must not exceed 50 characters");

        RuleFor(x => x.RecipientPostalCode)
            .MaximumLength(20).When(x => !string.IsNullOrEmpty(x.RecipientPostalCode));

        RuleFor(x => x.RecipientCountry)
            .Length(2).When(x => !string.IsNullOrEmpty(x.RecipientCountry))
            .WithMessage("Country code must be 2 characters (ISO 3166-1 alpha-2)");

        RuleFor(x => x.SpecialInstructions)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.SpecialInstructions));

        RuleFor(x => x.DeclaredValue)
            .GreaterThanOrEqualTo(0).When(x => x.DeclaredValue.HasValue)
            .LessThanOrEqualTo(10000).When(x => x.DeclaredValue.HasValue)
            .WithMessage("Declared value must be between 0 and 10000");
    }
}

/// <summary>
/// Validator for UpdateShipmentRequest.
/// </summary>
public sealed class UpdateShipmentRequestValidator : AbstractValidator<UpdateShipmentRequest>
{
    public UpdateShipmentRequestValidator()
    {
        RuleFor(x => x.WeightKg)
            .GreaterThan(0).When(x => x.WeightKg.HasValue)
            .LessThanOrEqualTo(70).When(x => x.WeightKg.HasValue);

        RuleFor(x => x.RecipientName)
            .MaximumLength(100).When(x => !string.IsNullOrEmpty(x.RecipientName));

        RuleFor(x => x.RecipientPhone)
            .Matches(@"^\+?[1-9]\d{1,14}$").When(x => !string.IsNullOrEmpty(x.RecipientPhone))
            .WithMessage("Phone number must be in valid E.164 format");

        RuleFor(x => x.RecipientAddress)
            .MaximumLength(200).When(x => !string.IsNullOrEmpty(x.RecipientAddress));

        RuleFor(x => x.RecipientCity)
            .MaximumLength(50).When(x => !string.IsNullOrEmpty(x.RecipientCity));

        RuleFor(x => x.RecipientPostalCode)
            .MaximumLength(20).When(x => !string.IsNullOrEmpty(x.RecipientPostalCode));

        RuleFor(x => x.RecipientCountry)
            .Length(2).When(x => !string.IsNullOrEmpty(x.RecipientCountry));

        RuleFor(x => x.SpecialInstructions)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.SpecialInstructions));

        RuleFor(x => x.DeclaredValue)
            .GreaterThanOrEqualTo(0).When(x => x.DeclaredValue.HasValue)
            .LessThanOrEqualTo(10000).When(x => x.DeclaredValue.HasValue);
    }
}

/// <summary>
/// Validator for UpdateStatusRequest.
/// </summary>
public sealed class UpdateStatusRequestValidator : AbstractValidator<UpdateStatusRequest>
{
    public UpdateStatusRequestValidator()
    {
        RuleFor(x => x.Status)
            .IsInEnum().WithMessage("Invalid shipment status");

        RuleFor(x => x.Reason)
            .NotEmpty().When(x => x.Status == ShipmentStatus.Delivered || x.Status == ShipmentStatus.Failed)
            .WithMessage("Reason is required for Delivered or Failed status");

        RuleFor(x => x.RecipientSignature)
            .NotEmpty().When(x => x.Status == ShipmentStatus.Delivered)
            .WithMessage("Recipient signature is required for Delivered status");

        RuleFor(x => x.Location)
            .SetValidator(new GpsCoordinatesValidator()).When(x => x.Location != null);
    }
}

/// <summary>
/// Validator for GPS coordinates (nullable).
/// </summary>
public sealed class GpsCoordinatesValidator : AbstractValidator<GpsCoordinates?>
{
    public GpsCoordinatesValidator()
    {
        RuleFor(x => x!.Latitude)
            .InclusiveBetween(-90, 90)
            .When(x => x != null);

        RuleFor(x => x!.Longitude)
            .InclusiveBetween(-180, 180)
            .When(x => x != null);
    }
}

/// <summary>
/// Validator for ScanDocumentRequest (additional rules beyond data annotations).
/// </summary>
public sealed class ScanDocumentRequestValidator : AbstractValidator<ScanDocumentRequest>
{
    public ScanDocumentRequestValidator()
    {
        RuleFor(x => x.ScanId)
            .NotEmpty()
            .Matches("^scan::[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$")
            .WithMessage("Scan ID must be in format scan::{Guid}");

        RuleFor(x => x.CourierId)
            .NotEmpty()
            .MaximumLength(50);

        RuleFor(x => x.Location)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.ScannedAtUtc)
            .LessThanOrEqualTo(DateTimeOffset.UtcNow.AddMinutes(5))
            .WithMessage("Scan time cannot be in the future");

        RuleFor(x => x.Notes)
            .MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.RecipientSignature)
            .NotEmpty().When(x => x.ScanType == ScanType.Delivered)
            .WithMessage("Recipient signature is required for Delivered scans");

        RuleFor(x => x.Notes)
            .NotEmpty().When(x => x.ScanType == ScanType.Failed)
            .WithMessage("Reason is required for Failed scans (provide in notes)");
    }
}

/// <summary>
/// Validator for ScanUploadRequest.
/// </summary>
public sealed class ScanUploadRequestValidator : AbstractValidator<ScanUploadRequest>
{
    public ScanUploadRequestValidator()
    {
        RuleFor(x => x.Scans)
            .NotEmpty().WithMessage("At least one scan is required")
            .Must(scans => scans.Count <= 100).WithMessage("Maximum 100 scans per upload");

        RuleForEach(x => x.Scans)
            .SetValidator(new ScanDocumentRequestValidator());
    }
}