// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 2 in COPILOT-LOG.md), then reviewed and changed by the student.
using System.Globalization;
using ApexPost.Api.DTOs;
using ApexPost.Api.Services;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Xunit;
using DtoScanType = ApexPost.Api.DTOs.ScanType;

namespace ApexPost.Specs.StepDefinitions;

[Binding]
public sealed class ScanSyncCopilotSteps(ServiceScenarioContext context)
{
    private static DateTimeOffset Parse(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    [Given("a shipment {string} is ready for courier upload at {string}")]
    public async Task GivenAShipmentIsReadyForCourierUploadAt(string trackingNumber, string at)
    {
        await using var db = context.Database.CreateContext();
        var shipment = TrackedShipment.Create(
            trackingNumber,
            2.5m,
            "Jane Example",
            "+15550100",
            "1 Example Street",
            "Springfield",
            createdAtUtc: Parse(at));

        await new SqlTrackedShipmentRepository(db).AddAsync(shipment);
        await new ApexPost.Infrastructure.Persistence.SqlUnitOfWork(db).SaveChangesAsync();
    }

    [Given("the service clock is set to {string}")]
    public void GivenTheServiceClockIsSetTo(string at) => context.Clock.Now = Parse(at);

    [Given("the courier performing the upload is {string}")]
    public void GivenTheCourierPerformingTheUploadIs(string courierId) => context.CourierId = courierId;

    [Given("the device has {int} offline scans queued for upload")]
    public void GivenTheDeviceHasOfflineScansQueuedForUpload(int count)
    {
        context.RecordedScans.Clear();
        for (var i = 0; i < count; i++)
        {
            context.RecordedScans.Add(NewScan(
                type: i == 0 ? DtoScanType.Pickup : DtoScanType.InTransit,
                at: context.Clock.Now.AddMinutes(-60 + (i * 10)),
                signature: null,
                index: i));
        }
    }

    [Given("the device has one invalid scan of type {string} at {int} minutes from now with signature {string}")]
    public void GivenTheDeviceHasOneInvalidScanOfTypeAtMinutesFromNowWithSignature(string scanType, int minutes, string signature)
    {
        context.RecordedScans.Clear();
        // Reviewed: Copilot used the magic words "empty" and "missing" in the Examples table; an empty cell is the honest way to say "no signature".
        var normalizedSignature = string.IsNullOrWhiteSpace(signature) ? null : signature;

        context.RecordedScans.Add(NewScan(
            type: Enum.Parse<DtoScanType>(scanType),
            at: context.Clock.Now.AddMinutes(minutes),
            signature: normalizedSignature,
            index: 0));
    }

    [Given("the same scans were already uploaded for {string}")]
    public async Task GivenTheSameScansWereAlreadyUploadedFor(string trackingNumber)
    {
        await UploadAsync(trackingNumber, context.RecordedScans);
    }

    [When("the courier uploads the queued scans for {string}")]
    public async Task WhenTheCourierUploadsTheQueuedScansFor(string trackingNumber)
    {
        try
        {
            context.LastUpload = await UploadAsync(trackingNumber, context.RecordedScans);
            context.LastError = null;
        }
        catch (Exception ex)
        {
            context.LastError = ex;
            context.LastUpload = null;
        }
    }

    [Then("the upload accepts {int} scans")]
    public void ThenTheUploadAcceptsScans(int count)
    {
        Assert.NotNull(context.LastUpload);
        Assert.Equal(count, context.LastUpload!.Accepted);
    }

    [Then("the upload skips {int} duplicates")]
    public void ThenTheUploadSkipsDuplicates(int count)
    {
        Assert.NotNull(context.LastUpload);
        Assert.Equal(count, context.LastUpload!.DuplicatesSkipped);
    }

    [Then("the upload rejects {int} scan with the reason {string}")]
    public void ThenTheUploadRejectsAScanWithReason(int count, string reason)
    {
        Assert.NotNull(context.LastUpload);
        Assert.Equal(count, context.LastUpload!.Rejected);
        Assert.Contains(reason, context.LastUpload.RejectionDetails[0].Reason);
    }

    [Then("the shipment stores {int} scans")]
    public async Task ThenTheShipmentStoresScans(int count)
    {
        await using var db = context.Database.CreateContext();
        Assert.Equal(count, await db.TrackedShipmentScans.CountAsync());
    }

    private ScanDocumentRequest NewScan(DtoScanType type, DateTimeOffset at, string? signature, int index) => new()
    {
        ScanId = $"scan::00000000-0000-0000-0000-{context.RecordedScans.Count + index:D12}",
        ScanType = type,
        CourierId = context.CourierId,
        Location = "Depot A",
        ScannedAtUtc = at,
        Notes = "scan recorded offline",
        RecipientSignature = signature
    };

    private async Task<ScanUploadResponse> UploadAsync(string trackingNumber, IReadOnlyList<ScanDocumentRequest> scans)
    {
        await using var db = context.Database.CreateContext();
        return await context.CreateUploadService(db).UploadScansAsync(
            trackingNumber,
            context.CourierId,
            new ScanUploadRequest { Scans = [.. scans] });
    }
}
