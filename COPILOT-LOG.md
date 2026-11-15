# AI-assisted test drafting log

## Honest note about the tool

The lab asks for **GitHub Copilot**. The first drafts below were made with **another AI coding assistant** (an AI coding assistant) as a **declared substitute**. This is stated openly and the drafts are not presented as Copilot output: Copilot output cannot be produced or invented here. The lab process is followed in full with the substitute tool: saved prompt, AI draft, human review (keep / change / reject with reasons), and proof by deliberate breakage that the tests can fail. The prompts are tool-neutral, so they can be pasted into Copilot Chat later if the instructor requires Copilot specifically; the "Copilot raw output" placeholders below are for that case only. **Tell the instructor about the substitution before the viva.**

Every AI-assisted file carries the comment `AI-ASSISTED: ...` on its first lines:
`tests/ApexPost.UnitTests/Client/ApexApiClientTests.cs`, `.../Outbox/OutboxServiceMoqTests.cs`, `.../Api/ScanUploadServiceMoqTests.cs`, `tests/ApexPost.Specs/Features/ScanSync.feature`, `tests/ApexPost.Specs/StepDefinitions/ScanSyncSteps.cs`.

---

## Prompt 1: unit tests for a service (ScanUploadService)

**Prompt**

> Write xUnit tests with Moq for `ScanUploadService` (file `src/ApexPost.Api/Services/ScanUploadService.cs`). Dependencies `ITrackedShipmentRepository` and `IUnitOfWork` must be mocked. Cover: new scan accepted and saved once; scan id already stored is skipped and nothing is saved; same id twice in one batch; mixed batch counts; the "scan time in the future" rule at 4, 5 and 6 minutes; Delivered without signature; scan of another courier gives ForbiddenException; unknown shipment gives NotFoundException; unique-key race gives ConflictException. Use Arrange/Act/Assert, `Method_Scenario_ExpectedResult` names, `[Theory]` for the boundary, and no `DateTime.Now`.

**What the assistant produced:** a Moq-based class with 13 test methods (17 cases with the theory rows) (`ScanUploadServiceMoqTests`) and the suggestion to inject a clock.

**Kept**

* `Verify(..., Times.Once)` / `Times.Never` on `SaveChangesAsync`: these prove the behaviour that matters for idempotency (nothing is written when everything is a duplicate).
* The 4 / 5 / 6 minute `[Theory]`: it pins the boundary (5 minutes allowed, 6 rejected).
* The race test (`UniqueKeyConflictException` becomes `ConflictException`): a branch no other test reached.

**Changed**

| Draft problem | Why it was wrong | Fix |
|---|---|---|
| The service read `DateTimeOffset.UtcNow`, so the 5-minute boundary test could not be exact | A test at the boundary would be flaky or only approximate | Production change: optional `TimeProvider` constructor parameter (default system clock); the tests use `ManualTimeProvider`. Documented in [TDD-LOG.md](TDD-LOG.md) section 6. |
| First duplicate test asserted only the counts | A service could report "1 duplicate" and still write | Added `Verify(UpdateAsync, Times.Never)` and `Verify(SaveChangesAsync, Times.Never)` |

**Rejected**

* Asserting full exception message strings for `ForbiddenException`: brittle, any wording change breaks the test; type plus the key fact is enough.
* Mocking `DbSet` to test duplicate detection: see TESTING-STRATEGY.md section 6; the real unique key is tested in `ApexPost.IntegrationTests`.

**What the mock does not prove:** that the real database primary key rejects a second scan with the same id. `SqlTrackedShipmentRepositoryTests` and the API endpoint test `PostScans_SameScanIdTwice_...` cover that on real EF Core.

**Copilot raw output:** *(paste here when you repeat this prompt in Copilot)*

---

## Prompt 2: Gherkin feature with step definitions (ScanSync)

**Prompt**

> Write a Reqnroll feature `ScanSync.feature` in business language for ApexPost courier scan upload, with a Background, at least one Scenario Outline with Examples, and scenarios: offline scans uploaded, a retried upload does not create duplicates, a mixed batch, invalid scans rejected with a reason, a courier cannot upload another courier's scans, and the offline outbox marking scans Synced or Failed. Then write the step definitions using context injection (no static fields) and BeforeScenario/AfterScenario hooks. Service level, no HTTP.

**What the assistant produced:** `ScanSync.feature` (7 scenarios, 8 test cases with the outline), `ScanSyncSteps`, `ServiceScenarioContext`, `DeviceFakes`, `Hooks`.

**Kept:** business wording ("the shipment history contains 2 scans"), one Background shared by all scenarios, regex steps that accept singular and plural ("1 scan is", "2 scans are").

**Changed**

* An Examples row `Delivered | -5 | Jane Example | (empty reason)` was a valid scan placed under "rejected": a scenario that asserts nothing meaningful. Removed.
* Plural/singular wording in steps ("1 scans are") was fixed with regex steps instead of rewording the scenarios into awkward English.

**Rejected**

* A single giant step "when I upload scans and check the result": it hides the Given/When/Then structure.
* Static shared state for the database between steps: replaced with per-scenario context injection and an `AfterScenario` cleanup hook.

**Copilot raw output:** *(paste here when you repeat this prompt in Copilot)*

---

## Other review decisions (from drafting the rest of the suite)

| # | Draft | Problem found by review or by running it | Decision |
|---|---|---|---|
| 1 | `TrackedShipmentCreationTests`: `"  APX-20250101  "` listed as a **valid** tracking number | The expectation copied what the implementation does (`Trim()`), but the rule says the number must match `^APX-\d{8}$`; padded input is invalid input | **Changed:** moved to the invalid list. A test that only repeats the implementation proves nothing. |
| 2 | Same class: weights 0.01 to 0.09 expected to be rejected | The implementation accepted them (a real defect) | **Kept the test**, fixed production (`e3d23eb`). |
| 3 | `PatchStatus_LegalThenIllegalTransition` sent Delivered without a `reason`, expected 409 | API answered 400: the DTO requires a reason for Delivered, before the transition rule is reached | **Changed the payload** (wrong input, not wrong behaviour); noted in README that the DTO rule is stricter than the domain rule. |
| 4 | `TestJwt.Create(expires: past)` | `notBefore` was after `expires`, so the helper threw instead of making an expired token | **Fixed the helper** (notBefore = expiry minus 2 h). |
| 5 | Client test "timeout": considered asserting a retry happens | Retry is not in `ApexApiClient`; it is in the handler pipeline (Polly), outside this class | **Rejected** that assertion; the test asserts the client lets `TaskCanceledException` propagate so the resilience layer can act. |

---

## Proof that generated tests are meaningful (deliberate breakage, then restore)

Real runs; raw output in `docs/evidence/`. Each mutation was applied with `sed`, the tests were run, the file was restored with `git checkout -- <file>` and `git status` showed it clean again.

| # | Production change (deliberately wrong) | Tests that failed | Evidence |
|---|---|---|---|
| 1 | `ScanUploadService`: `if (knownScanIds.Contains(...))` replaced by `if (false && ...)` (duplicate check disabled) | 3 Moq unit tests (`..._ScanIdAlreadyStored_...`, `..._SameScanIdTwiceInOneBatch_...`, `..._MixedBatch_...`) and 2 Reqnroll scenarios ("A retried upload does not create duplicates", "A batch mixing new and already known scans...") | [mutation-1-duplicate-check.txt](docs/evidence/mutation-1-duplicate-check.txt) |
| 2 | `TrackedShipment`: allowed transition `Delivered -> InTransit` | `UpdateStatus_Delivered_CannotMoveBackToInTransit` and the matrix case `(Delivered, InTransit, legal: False)` | [mutation-2-3-transition-and-outbox.txt](docs/evidence/mutation-2-3-transition-and-outbox.txt) |
| 3 | `OutboxService`: `success ? Synced : Failed` swapped | 3 Moq unit tests and the Reqnroll scenario "The outbox marks uploaded scans as synced and failed ones as failed" | same file |
| 4 | `HeavyParcelSurcharge`: threshold check `<=` changed to `<` (exactly 30 kg gets charged) | 2 unit tests, 1 API integration test and 1 Reqnroll scenario outline row (weight 30) | [mutation-4-surcharge-threshold.txt](docs/evidence/mutation-4-surcharge-threshold.txt) |
| 5 | `ApexApiClient`: route `api/v2/...` changed to `api/v1/...` (an AI-assisted test class) | `UploadScansAsync_AnyCall_PostsToV2ScansRouteWithCamelCaseJson` | [mutation-5-client-route.txt](docs/evidence/mutation-5-client-route.txt) |

Each break (mutations 1, 3 and 4 at three levels) was caught by at least two different test levels, which is what the pyramid is meant to give.

---

## Prompt 3: typed API client tests (fake HttpMessageHandler)

**Prompt**

> Write xUnit tests for `ApexApiClient.UploadScansAsync` using a fake `HttpMessageHandler` (no real network). Cover: 200 with JSON, route and camelCase body, special characters in the tracking number, empty body, malformed JSON, unknown JSON fields, 404 with ProblemDetails, 500 with plain text, a theory of error status codes, a 400 validation problem, HttpClient timeout, network failure and caller cancellation.

**What the assistant produced:** `ApexApiClientTests` (14 methods, 19 cases) and `StubHttpMessageHandler`.

**Kept:** the status-code theory (one method, six cases); the test that the tracking number is URL-escaped; the cancellation test where the handler waits on the caller token.

**Changed:** the timeout test originally also expected a retry; retry lives in the Polly handler pipeline, not in `ApexApiClient`, so it now asserts that `TaskCanceledException` (with `TimeoutException` inside) propagates to the resilience layer.

**Rejected:** a test with a real `localhost` server (breaks the no-network rule); asserting log output (couples the test to wording).

**What the fake does not prove:** that the real Polly pipeline retries or opens the circuit; that is shown in the Week 4 runtime evidence, not in unit tests.

---

## If your course requires GitHub Copilot itself (fill-in pack)

No Copilot output is invented anywhere in this repository. If your instructor requires Copilot specifically, do this once; it takes about 20 minutes and every field below must be your own words.

1. In VS Code with Copilot Chat, open the file named in each prompt above and paste the prompt **unchanged**.
2. Paste Copilot's raw answer under "Copilot raw output" in that prompt's section.
3. Write three short lists under it: **Kept**, **Changed**, **Rejected**, each with the reason (weak assertion, wrong expected value, wrong mock setup, missing edge case, copied the implementation, flaky time use).
4. Save Copilot's tests in a **new** file next to the existing one (for example `ScanUploadServiceCopilotTests.cs`), put `// COPILOT-ASSISTED: drafted with GitHub Copilot, reviewed by <your name>` on the first line, and keep only the tests you accept.
5. Do one more deliberate-breakage proof on one of those tests (break the production line, run the test, show the failure, restore with `git checkout -- <file>`) and add a row to the proof table above.
6. Commit as `docs: add real Copilot drafts and review decisions`.

Prompt 4 (optional extra, for the viva): *"Write a Reqnroll feature `ShipmentLifecycle.feature` for ApexPost shipments: a courier moves a parcel from Created to Delivered, a step cannot be skipped, a delivered parcel cannot move back (Scenario Outline), only Created parcels can be deleted. Use a Background and context-injected step definitions for an ASP.NET Core WebApplicationFactory API."*

### Message to send to your instructor if you cannot use Copilot

> I could not use GitHub Copilot (no access). I used another AI coding assistant (another AI coding assistant) for the AI-assisted drafting step and kept the same process: saved prompts, human review with keep/change/reject decisions, `AI-ASSISTED` markers, and five deliberate-breakage proofs (docs/evidence). It is stated openly in COPILOT-LOG.md. Is this accepted for the Copilot criterion, or should I repeat prompts 1 and 2 in Copilot?

---

# Real GitHub Copilot session (student ran the prompts)

Status: **all three Copilot prompts are done and reviewed below** (prompt 1 service unit tests, prompt 2 Gherkin feature + steps, prompt 3 HTTP client tests). The earlier sections of this log are the another AI coding assistant drafts and stay as the declared substitute; this section is the Copilot one.

Files from Copilot: `tests/ApexPost.Specs/Features/ScanSyncCopilot.feature`, `tests/ApexPost.Specs/StepDefinitions/ScanSyncCopilotSteps.cs` (both marked `COPILOT-ASSISTED`).

## Prompt 2 (Copilot): ScanSyncCopilot.feature and steps

**Prompt:** the one in the section "Prompt 2" of the pack (Reqnroll feature in business language, Background, Scenario Outline with Examples, constructor-injected `ServiceScenarioContext`, service level, no HTTP).

**Copilot raw output (as received, before review):** a feature "Courier scan upload sync" with a Background (shipment ready, service clock, courier id) and 3 scenarios (offline scans uploaded, retried upload does not create duplicates, Scenario Outline for invalid scans with an Examples table), plus a `ScanSyncCopilotSteps` binding class using `ServiceScenarioContext` through its constructor. The Examples table used the magic words `empty` and `missing` in the signature column. The project file also contained `<ReqnrollFeatureFile Remove="Features\ScanSyncCopilot.feature" />`, which switched the feature off.

**Kept**

* The Background, the three scenarios and the business wording ("the upload accepts 2 scans", "the shipment stores 0 scans"): they read like the business rule and match the agreed behaviour.
* Constructor injection of `ServiceScenarioContext` (no static fields) and the Arrange-in-`Given` / Act-in-`When` / Assert-in-`Then` split.
* The `Then` steps assert `NotNull(LastUpload)` before reading it, so an upload that threw fails with a clear reason instead of a null reference.

**Changed**

| Copilot draft | Problem | My change |
|---|---|---|
| `Remove="Features\ScanSyncCopilot.feature"` in `ApexPost.Specs.csproj` | **The feature never ran**: a test that is not executed proves nothing, and the suite stayed green by ignoring it | Removed the exclusion; the feature now runs: 4 more scenario results, **37 Specs tests pass** |
| Signature column `empty` / `missing` | Magic words that only work because the step file special-cases them; a reader cannot tell what they mean | Empty table cell, and the step now uses `string.IsNullOrWhiteSpace(signature)` |
| Comment `AI-ASSISTED: drafted for the service-level ...` | Wrong marker and no tool named | `COPILOT-ASSISTED` marker on the feature and the steps class |
| `Parse`, `NewScan` and `UploadAsync` copied from `ScanSyncSteps` | Duplicated helper code that can drift apart | Accepted for now (two small private helpers), listed as a refactoring candidate |

**Rejected / missing (edge cases Copilot did not draft)**

* a courier uploading another courier's scans (403 / `ForbiddenException`),
* a mixed batch of new and already known scans,
* the offline outbox (Pending to Synced / Failed).

These stay covered by my `ScanSync.feature` (another AI coding assistant draft); I did not ask Copilot for them.

**What the feature does not prove:** it runs at service level on SQLite, so HTTP status codes, JWT roles and SQL Server behaviour are not part of it (the API features and the opt-in SQL Server tests cover those).

### Proof that Copilot's scenarios can fail (deliberate breakage, then restore)

Raw output: [mutation-6-7-copilot-feature.txt](docs/evidence/mutation-6-7-copilot-feature.txt).

| # | Production change (deliberately wrong) | Copilot scenario that failed |
|---|---|---|
| 6 | `ScanUploadService`: duplicate check disabled (`if (false && ...)`) | "A retried upload does not create duplicates" |
| 7 | `ScanUploadService`: future-scan rule disabled | "Invalid scans are rejected with a reason", row Pickup / 30 minutes |

Both files were restored with `git checkout -- <file>` and `git status` was clean afterwards.

## Prompt 1 (Copilot): ScanUploadServiceCopilotTests

**Prompt:** the one in the section "Prompt 1" of the pack (xUnit + Moq tests for `ScanUploadService`, new file, no edits to existing files, `ManualTimeProvider`, AAA, `Method_Scenario_ExpectedResult`).

**Copilot raw output (as received, before review):** a new file `tests/ApexPost.UnitTests/Api/ScanUploadServiceCopilotTests.cs` (marker line included) with 11 test cases: new scan accepted and saved once; stored scan id skipped and nothing saved; same id twice in one batch; mixed batch (new, duplicate, future); a `[Theory]` for the 4 / 5 / 6 minute future boundary; Delivered without signature rejected; scan of another courier gives `ForbiddenException`; unknown shipment gives `NotFoundException`; unique-key race gives `ConflictException`. It compiled and all 11 passed on the first run.

**Kept**

* The structure: constructor-set-up mocks, one `CreateSut()`, AAA comments, correct `Method_Scenario_ExpectedResult` names.
* The boundary `[Theory]` (4 and 5 minutes allowed, 6 rejected) with the injected `ManualTimeProvider`: no `DateTime.Now`.
* `Verify(..., Times.Never)` on `SaveChangesAsync` in the duplicate, Delivered, Forbidden and NotFound tests: these are what prove "nothing is saved".
* The race test: a branch (`UniqueKeyConflictException` to `ConflictException`) that is easy to forget.

**Changed**

| Copilot draft | Problem | My change |
|---|---|---|
| Shipment created inline in the constructor `Setup` | No test could inspect what was really stored | Shipment kept in a field `_shipment` |
| "Same scan id twice" asserted only the counts | A buggy service could report 1 accepted and still store two scans (weak assertion) | Added `Assert.Single(_shipment.Scans)` |
| `Verify(UpdateAsync(It.IsAny<TrackedShipment>()))` | Passes for any shipment, so it would not notice updating the wrong one | Verify with the exact instance `_shipment` |

**Rejected / missing (edge cases Copilot did not draft)**

* A token without a `courierId` claim (null, empty, spaces): **added** a `[Theory]` with three cases that expects `ForbiddenException` and no save.
* Not drafted and not added here: mapping of the scan type to the domain enum and the "clock moves" test; they exist in my `ScanUploadServiceMoqTests`.

**Overlap to be honest about:** the cases in Copilot's file are nearly the same as in my another AI coding assistant draft `ScanUploadServiceMoqTests` (the prompt was derived from it). I kept both because the Copilot file is the lab deliverable; mine uses strict mocks and also covers the clock and enum mapping.

**What the mock does not prove:** it proves the service skips a duplicate and never calls `SaveChangesAsync`; it does not prove that the real primary key on SQL Server or SQLite rejects a duplicate id. The SQLite repository tests, the API tests and the opt-in SQL Server test cover that.

### Proof that Copilot's unit tests can fail

Raw output: [mutation-8-9-copilot-unit-tests.txt](docs/evidence/mutation-8-9-copilot-unit-tests.txt).

| # | Production change (deliberately wrong) | Copilot test that failed |
|---|---|---|
| 8 | `ScanUploadService`: future check `>` changed to `>=` | `UploadScansAsync_ScanTimeInTheFutureAtBoundaryMinutes_AppliesFiveMinuteSkew(minutesAhead: 5, accepted: True)` |
| 9 | `ScanUploadService`: courier ownership check disabled | `UploadScansAsync_ScanOfAnotherCourier_ThrowsForbiddenException` |

Both files were restored with `git checkout -- <file>` and `git status` was clean afterwards.

## Prompt 3 (Copilot): ApexApiClientCopilotTests

**Prompt:** the one in the section "Prompt 3" of the pack (xUnit tests for `ApexApiClient.UploadScansAsync` with the existing `StubHttpMessageHandler`: 200 JSON, 404 ProblemDetails, 500 plain text, timeout, cancellation, malformed JSON; new file; marker line).

**Copilot raw output (as received, before review):** a new file `tests/ApexPost.UnitTests/Client/ApexApiClientCopilotTests.cs` with 6 tests (marker line included). It compiled and all 6 passed on the first run.

**Kept**

* One test per requested case, with Arrange/Act/Assert comments and clear `Method_Scenario_ExpectedResult` names.
* The 404 test asserts four fields of the problem (`StatusCode`, title as message, `Detail`, `ProblemType`), not just "an exception was thrown".
* The timeout test asserts that the `TimeoutException` inside the `TaskCanceledException` survives, and the cancellation test uses a handler that waits for the caller token: no real network, no sleeping.

**Changed (added in review)**

| Gap in Copilot's draft | Why it matters | My change |
|---|---|---|
| No test looked at the **request** | A client posting to the wrong route or with wrong JSON names would pass all six tests | Added `PostsToTheV2ScansRouteWithCamelCaseJson` (method, full URL, JSON property name) |
| Only 404 and 500 were covered | The courier app also meets 400, 401, 403, 409 and 503 | Added a `[Theory]` with 5 status codes |

**Rejected / not added**

* Retry behaviour: it lives in the Polly handler pipeline, not in `ApexApiClient`; asserting it here would test the wrong class (same decision as in the another AI coding assistant draft).
* Unknown JSON fields and a pure network failure (`HttpRequestException`): not drafted by Copilot and not added; they stay covered by my `ApexApiClientTests`.

**Overlap to be honest about:** Copilot saw my `ApexApiClientTests.cs` in the same folder, and its six tests are almost line-for-line the same cases and data. They are valid and I kept them because the file is the lab deliverable, but they add no coverage beyond my draft; the two review additions above are what is new.

**What the fake handler does not prove:** that the real Polly pipeline retries or opens the circuit, or that the real API returns these bodies. The API integration tests and the Week 4 runtime evidence cover that.

### Proof that Copilot's client tests can fail

Raw output: [mutation-10-11-copilot-client-tests.txt](docs/evidence/mutation-10-11-copilot-client-tests.txt).

| # | Production change (deliberately wrong) | Test that failed |
|---|---|---|
| 10 | `ApexApiClient`: route `api/v2/...` changed to `api/v1/...` | `UploadScansAsync_AnyCall_PostsToTheV2ScansRouteWithCamelCaseJson` (my addition) |
| 11 | `ApexApiClient`: ProblemDetails title ignored when building the exception | `UploadScansAsync_404WithProblemDetails_ThrowsApiExceptionWithProblemInfo` (Copilot's original test) |

Both files were restored with `git checkout -- <file>` and `git status` was clean afterwards.

## Summary of the real Copilot work

| Prompt | Files | Tests | Review result |
|---|---|---|---|
| 1 service unit tests | `ScanUploadServiceCopilotTests.cs` | 14 cases | 3 changes, 1 added theory, proofs 8 and 9 |
| 2 Gherkin feature + steps | `ScanSyncCopilot.feature`, `ScanSyncCopilotSteps.cs` | 4 scenarios | feature was excluded from the build and is now running, magic words removed, proofs 6 and 7 |
| 3 HTTP client tests | `ApexApiClientCopilotTests.cs` | 12 cases | 2 additions (request check, status theory), proofs 10 and 11 |

All three files carry `COPILOT-ASSISTED`; the another AI coding assistant drafts earlier in this log remain, declared as such.

---

# Copilot round 2: areas no earlier draft covers (prompts 4 and 5)

Why this round exists: Copilot prompts 1 and 3 targeted code that I had already drafted tests for, and Copilot saw those drafts, so the results overlapped. Prompts 4 and 5 target code that has **no tests at all yet** (`SyncService`, `ApiKeyAuthorizeAttribute`) and tell Copilot **not to read the existing tests**, so the output is independent work. Status: **both prompts were run in Copilot and are reviewed below.**

## Prompt 4: SyncService (batches of 20, Synced/Failed, circuit)

Attach: `src/ApexPost.Courier.Console/Services/SyncService.cs`, `src/ApexPost.Courier.Console/Services/ApexApiClient.cs`, `src/ApexPost.Courier.Console/Services/ResilienceStateTracker.cs`. Do NOT attach any file from `tests/`.

```text
Create a NEW file tests/ApexPost.UnitTests/Client/SyncServiceCopilotTests.cs. Do not edit or delete any existing file, and do not read or copy any file under tests/ (write the tests from the production code only).

The first line of the file must be exactly:
// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 4 in COPILOT-LOG.md), then reviewed by the student.

Write xUnit tests with Moq for SyncService (namespace ApexPost.UnitTests.Client, class SyncServiceCopilotTests). Mock IApexApiClient, ICourierDatabase, IResilienceStateTracker and ITokenStorage; build SyncOptions through Options.Create. Cover: 45 pending scans of one shipment are uploaded in batches of 20 (Verify UploadScansAsync is called 3 times with 20, 20 and 5 scans); a batch that the API accepts marks its scans Synced; an ApiException with status 500 marks the scans Failed; a 409 leaves the scans pending (deferred); an ApiException with status 401 is handled without throwing out of SyncPendingScansAsync; when there are no pending scans UploadScansAsync is never called; cancellation is honoured. Use Arrange/Act/Assert comments, Method_Scenario_ExpectedResult names, Setup/ReturnsAsync, It.Is/It.IsAny and Verify with Times. No real network, no Thread.Sleep, no DateTime.Now. After creating the file, run dotnet test tests/ApexPost.UnitTests and fix compile errors in YOUR new file only.
```

## Prompt 5: ApiKeyAuthorizeAttribute (partner API key, security rule)

Attach: `src/ApexPost.Api/Filters/ApiKeyAuthorizeAttribute.cs`. Do NOT attach any file from `tests/`.

```text
Create a NEW file tests/ApexPost.UnitTests/Api/ApiKeyAuthorizeAttributeCopilotTests.cs. Do not edit or delete any existing file, and do not read or copy any file under tests/.

The first line of the file must be exactly:
// COPILOT-ASSISTED: drafted with GitHub Copilot (prompt 5 in COPILOT-LOG.md), then reviewed by the student.

Write xUnit tests for ApiKeyAuthorizeAttribute (namespace ApexPost.UnitTests.Api, class ApiKeyAuthorizeAttributeCopilotTests). Build a DefaultHttpContext whose RequestServices contains an IConfiguration created with ConfigurationBuilder().AddInMemoryCollection(...), and an AuthorizationFilterContext around it. Cover: no key configured on the server gives a 500 problem result; missing X-Api-Key header gives 401; empty header gives 401; wrong key gives 401; the key configured as PartnerApiKey is accepted (context.Result stays null); a key configured under PartnerApiKeys:SomePartner is accepted; a key that is a prefix of the real key, and a longer key that starts with the real key, are both rejected. The attribute also reads the environment variable APEXPOST_PARTNER_API_KEY: make the tests deterministic, so no test may depend on or leave behind that variable. Use only obviously fake keys such as "test-key-123". Use Arrange/Act/Assert comments and Method_Scenario_ExpectedResult names. After creating the file, run dotnet test tests/ApexPost.UnitTests and fix compile errors in YOUR new file only.
```

## What to look for when you review (these are the likely weak points, so the review is real)

* **Prompt 5, shared state:** the attribute reads the process-wide environment variable `APEXPOST_PARTNER_API_KEY`. If Copilot sets it with `Environment.SetEnvironmentVariable` and does not restore it, tests can affect each other when xUnit runs them in parallel (determinism rule). Decide: keep, wrap in a collection with `DisableParallelization`, or change.
* **Prompt 5, weak assertions:** "returns 401" must also check the problem title or status, not only that `Result` is not null; and a "valid key" test must assert `Result` is null.
* **Prompt 4, loose mocks:** a mock that returns the default for everything can make "marks Failed" pass without proving anything. Check that each outcome is verified with `Verify(..., Times.Once)` on the exact update call.
* **Prompt 4, hidden time dependence:** `SyncService` has circuit timers; any `Task.Delay` or `Thread.Sleep` in the draft is a flaky test (reject it).
* **Both:** copied production logic in the expected values (for example computing the expected batch count with the same formula the service uses) proves nothing.

After the files exist: run `dotnet test`, do one breakage proof per file (for example change the batch size constant, or compare the key with `==` on a shortened key), add the Kept / Changed / Rejected tables here, and commit.

---

## Prompt 4 (Copilot, round 2): SyncServiceCopilotTests

**Copilot raw output (as received, before review):** a new file `tests/ApexPost.UnitTests/Client/SyncServiceCopilotTests.cs` with 5 tests: 45 scans uploaded in batches of 20, 20 and 5 and all marked Synced; a batch with one rejection marks only that scan Failed; a 409 defers the batch; a 401 clears the token and returns an error; a cancelled token throws. It compiled. **4 passed and 1 failed on the first run.**

**The failing test found a real production bug.** `SyncPendingScansAsync_Unauthorized401_ClearsTokenAndReturnsError` expected `SyncState.Error` after a 401 but got `Idle`. The 401 handler in `SyncService` does set `Error`, but the last line of the method resets the state with `SetStatus(SyncState.Idle)`, so the Error state could never be seen; the Sync Status screen even has a colour for `Error`, so it was meant to be visible. I did not weaken the test: I committed it as a red commit and fixed production in a green commit (`RED:` / `GREEN:` in the log: the final state is `Error` after an authentication failure, otherwise `Idle`). This is a second TDD-style cycle that came from a Copilot test.

**Kept**

* The batch test: it checks the call pattern (twice with 20 scans, once with 5) and that all 45 scans are marked Synced, so a batch-size bug is caught.
* The 409 test with `Times.Never` on both Mark calls: it proves "deferred" means the scans stay pending.
* The 401 test (see above) and the cancellation test with `Times.Never` on the API and database calls.

**Changed (added in review)**

| Gap in Copilot's draft | My change |
|---|---|
| The prompt asked for a 500 case and an empty queue; Copilot skipped both | Added `ServerError500_CountsScansAsFailedAndNeverMarksThemSynced` and `NoPendingScans_NeverCallsTheApi` |
| Marker did not name the prompt | Marker names prompt 4 and says "reviewed and extended" |

**Rejected / not done:** no `Task.Delay` or `Thread.Sleep` were used (checked, nothing to reject); the circuit-breaker timing path (`CircuitBreakerResetTimeout`) is not tested here because it depends on `DateTimeOffset.UtcNow` inside the service; it needs a `TimeProvider` first, listed as a future testability change.

**What the mocks do not prove:** they prove which calls `SyncService` makes; they do not prove that the real Couchbase Lite database changes the stored status (the Courier.Offline tests cover that) or that the real API accepts a batch of 20.

## Prompt 5 (Copilot, round 2): ApiKeyAuthorizeAttributeCopilotTests

**Copilot raw output (as received, before review):** a new file with 9 test cases: no key configured gives a 500 problem; missing header, empty header and wrong key each give 401 with the exact title and detail; `PartnerApiKey` and `PartnerApiKeys:SomePartner` are accepted and leave `Result` null; a prefix of the key and a longer key are rejected (`[Theory]`); the environment-variable key is accepted. It compiled and all 9 passed on the first run.

**Kept:** the exact-assertion style (status, `ProblemDetails.Title` and `Detail`, not just "a result exists"); the prefix / longer-key theory, which is the case a naive string comparison gets wrong; `Assert.Null(filterContext.Result)` for accepted keys; fake keys only.

**Changed**

| Copilot draft | Problem | My change |
|---|---|---|
| `Environment.SetEnvironmentVariable(..., null)` repeated in six tests, restored in only one | Shared mutable process state (determinism rule): a test could leave the variable changed, and the result depended on the machine | One constructor clears the variable, `Dispose` restores the original, and the class is in a named collection so xUnit does not run classes that touch it in parallel |
| Marker said only "reviewed by the student" | Not accurate after my changes | Marker says "reviewed and changed" |

**Rejected / not covered:** a test that the comparison is constant-time: it cannot be observed from a unit test, so it is stated as not tested (the SHA-256 comparison is documented in SECURITY.md).

**What the tests do not prove:** the attribute is only the filter; they do not prove that the real controller carries it (the API endpoint tests with a partner key cover the wiring).

### Proof that the round 2 tests can fail

Raw output: [mutation-12-14-copilot-round2.txt](docs/evidence/mutation-12-14-copilot-round2.txt).

| # | Production change (deliberately wrong) | Copilot test that failed |
|---|---|---|
| 12 | `SyncService`: `Chunk(batchSize)` changed to `Chunk(batchSize + 1)` | `FortyFivePendingScansInThreeBatches_UsesThreeUploadsAndMarksAllSynced` |
| 13 | `ApiKeyAuthorizeAttribute`: any non-empty key accepted | the wrong-key test and both prefix / longer-key cases |
| 14 | the 401 fix undone (Idle overwrites Error) | `Unauthorized401_ClearsTokenAndReturnsError` |

All production files were restored with `git checkout -- <file>` and `git status` was clean afterwards.

## Updated summary of the real Copilot work

| Prompt | File | Result |
|---|---|---|
| 1 service unit tests | `ScanUploadServiceCopilotTests.cs` | 3 changes, 1 added theory, proofs 8 and 9 |
| 2 Gherkin feature + steps | `ScanSyncCopilot.feature`, `ScanSyncCopilotSteps.cs` | excluded-from-build problem fixed, magic words removed, proofs 6 and 7 |
| 3 HTTP client tests | `ApexApiClientCopilotTests.cs` | request check and status theory added, proofs 10 and 11 |
| 4 SyncService | `SyncServiceCopilotTests.cs` | **found a production bug** (fixed), 2 tests added, proofs 12 and 14 |
| 5 ApiKeyAuthorizeAttribute | `ApiKeyAuthorizeAttributeCopilotTests.cs` | shared environment state fixed, proof 13 |

Prompts 4 and 5 target code with no earlier tests, so unlike prompts 1 and 3 they do not overlap my another AI coding assistant drafts.
