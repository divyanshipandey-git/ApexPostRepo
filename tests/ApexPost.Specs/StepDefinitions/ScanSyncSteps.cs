using System.Globalization;
using ApexPost.Api.DTOs;
using ApexPost.Api.Exceptions;
using ApexPost.Courier.Domain;
using ApexPost.Courier.Offline.Services;
using ApexPost.Domain.Entities;
using ApexPost.Infrastructure.Persistence.Repositories;
using ApexPost.Specs.Support;
using Microsoft.EntityFrameworkCore;
using Reqnroll;
using Xunit;
using DtoScanType = ApexPost.Api.DTOs.ScanType;
using DeviceScanType = ApexPost.Courier.Domain.ScanType;

namespace ApexPost.Specs.StepDefinitions;

// AI-ASSISTED: drafted with an AI assistant (another AI coding assistant), then reviewed and changed by a human; see COPILOT-LOG.md.
/// <summary>Steps of ScanSync.feature. Runs at SERVICE level: real services and repositories, no HTTP.</summary>
[Binding]
public sealed class ScanSyncSteps(ServiceScenarioContext context)
{
    private static DateTimeOffset Parse(string text) =>
        DateTimeOffset.Parse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal);

    // ------------------------------------------------------------------ background

    [Given("shipment {string} was registered at {string}")]
    public async Task GivenShipmentWasRegisteredAt(string trackingNumber, string at)
    {
        await using var db = context.Database.CreateContext();
        var shipment = TrackedShipment.Create(trackingNumber, 2.5m, "Jane Example", "+15550100", "1 Example Street", "Springfield", createdAtUtc: Parse(at));
        await new SqlTrackedShipmentRepository(db).AddAsync(shipment);
        await new ApexPost.Infrastructure.Persistence.SqlUnitOfWork(db).SaveChangesAsync();
    }

    [Given("the time is now {string}")]
    public void GivenTheTimeIsNow(string at) => context.Clock.Now = Parse(at);

    [Given("I am courier {string}")]
    public void GivenIAmCourier(string courierId) => context.CourierId = courierId;

    // ------------------------------------------------------------------ recording scans on the device

    [Given(@"I recorded (\d+) scans? while offline")]
    public void GivenIRecordedScansWhileOffline(int count)
    {
        for (var i = 0; i < count; i++)
        {
            context.RecordedScans.Add(NewScan(
                i == 0 ? DtoScanType.Pickup : DtoScanType.InTransit,
                context.Clock.Now.AddMinutes(-60 + (i * 10)),
                signature: null,
                index: i));
        }
    }

    [Given("I recorded a {string} scan {int} minutes from now with signature {string}")]
    public void GivenIRecordedAScanFromNow(string type, int minutes, string signature) =>
        context.RecordedScans.Add(NewScan(
            Enum.Parse<DtoScanType>(type),
            context.Clock.Now.AddMinutes(minutes),
            string.IsNullOrEmpty(signature) ? null : signature,
            index: 0));

    [Given("one of my scans carries the courier id {string}")]
    public void GivenOneOfMyScansCarriesTheCourierId(string courierId)
    {
        var last = context.RecordedScans[^1];
        context.RecordedScans[^1] = new ScanDocumentRequest
        {
            ScanId = last.ScanId, ScanType = last.ScanType, CourierId = courierId, Location = last.Location,
            ScannedAtUtc = last.ScannedAtUtc, Notes = last.Notes, RecipientSignature = last.RecipientSignature
        };
    }

    // ------------------------------------------------------------------ uploading

    [Given(@"I have already uploaded my scans for ""(.*)""")]
    public async Task GivenIHaveAlreadyUploaded(string trackingNumber) => await UploadAsync(trackingNumber, context.RecordedScans);

    [Given("only my first scan has already been uploaded")]
    public async Task GivenOnlyMyFirstScanHasAlreadyBeenUploaded() =>
        await UploadAsync("APX-20250101", context.RecordedScans.Take(1).ToList());

    [When(@"I upload my scans for ""(.*)""")]
    public async Task WhenIUploadMyScansFor(string trackingNumber) => context.LastUpload = await UploadAsync(trackingNumber, context.RecordedScans);

    [When(@"I try to upload my scans for ""(.*)""")]
    public async Task WhenITryToUploadMyScansFor(string trackingNumber)
    {
        try
        {
            context.LastUpload = await UploadAsync(trackingNumber, context.RecordedScans);
        }
        catch (Exception ex)
        {
            context.LastError = ex;
        }
    }

    [Then(@"(\d+) scans? (?:is|are) accepted")]
    public void ThenScansAreAccepted(int count) => Assert.Equal(count, context.LastUpload!.Accepted);

    [Then(@"(\d+) duplicates? (?:is|are) skipped")]
    public void ThenDuplicatesAreSkipped(int count) => Assert.Equal(count, context.LastUpload!.DuplicatesSkipped);

    [Then(@"(\d+) scans? (?:is|are) rejected with the reason ""(.*)""")]
    public void ThenScansAreRejected(int count, string reason)
    {
        Assert.Equal(count, context.LastUpload!.Rejected);
        Assert.Contains(reason, context.LastUpload.RejectionDetails[0].Reason);
    }

    [Then("the upload is forbidden")]
    public void ThenTheUploadIsForbidden() => Assert.IsType<ForbiddenException>(context.LastError);

    [Then(@"the shipment history contains (\d+) scans?")]
    public async Task ThenTheShipmentHistoryContains(int count)
    {
        await using var db = context.Database.CreateContext();
        Assert.Equal(count, await db.TrackedShipmentScans.CountAsync());
    }

    // ------------------------------------------------------------------ device outbox

    [Given(@"(\d+) scans? (?:is|are) waiting in the offline outbox")]
    public async Task GivenScansAreWaitingInTheOutbox(int count)
    {
        for (var i = 0; i < count; i++)
        {
            await context.DeviceScans.CreateAsync(ShipmentScan.Create(
                "APX-20250101", DeviceScanType.InTransit, context.CourierId, "Depot A", context.Clock.Now.AddMinutes(i)));
        }
    }

    [Given("the server accepts only the first scan")]
    public void GivenTheServerAcceptsOnlyTheFirstScan() => context.Uploader.AcceptFirst = 1;

    [Given("the server rejects everything")]
    public void GivenTheServerRejectsEverything() => context.Uploader.AcceptFirst = 0;

    [Given("the server is back online")]
    public void GivenTheServerIsBackOnline() => context.Uploader.AcceptAll = true;

    [Given("the outbox has run once")]
    [When("the outbox runs")]
    public async Task WhenTheOutboxRuns() => await new OutboxService(context.DeviceScans, context.Uploader).ProcessPendingAsync();

    [Then(@"(\d+) scans? (?:is|are) marked (Synced|Failed)")]
    public void ThenScansAreMarked(int count, SyncStatus status) =>
        Assert.Equal(count, context.DeviceScans.All.Count(s => s.SyncStatus == status));

    // ------------------------------------------------------------------ helpers

    private ScanDocumentRequest NewScan(DtoScanType type, DateTimeOffset at, string? signature, int index) => new()
    {
        ScanId = $"scan::00000000-0000-0000-0000-{context.RecordedScans.Count + index:D12}",
        ScanType = type,
        CourierId = context.CourierId,
        Location = "Depot A",
        ScannedAtUtc = at,
        RecipientSignature = signature,
        Notes = "scan recorded offline"
    };

    private async Task<ScanUploadResponse> UploadAsync(string trackingNumber, IReadOnlyList<ScanDocumentRequest> scans)
    {
        // A new DbContext per upload, like one HTTP request per upload.
        await using var db = context.Database.CreateContext();
        return await context.CreateUploadService(db).UploadScansAsync(
            trackingNumber, context.CourierId, new ScanUploadRequest { Scans = [.. scans] });
    }
}
