# AI-assisted test drafting log

## Honest note about the tool

The lab asks for **GitHub Copilot**. The student has no Copilot access, so the AI-assisted step was done with **another AI coding assistant** (an AI coding assistant) as a **declared substitute**. This is stated openly and the drafts are not presented as Copilot output: Copilot output cannot be produced or invented here. The lab process is followed in full with the substitute tool: saved prompt, AI draft, human review (keep / change / reject with reasons), and proof by deliberate breakage that the tests can fail. The prompts are tool-neutral, so they can be pasted into Copilot Chat later if the instructor requires Copilot specifically; the "Copilot raw output" placeholders below are for that case only. **Tell the instructor about the substitution before the viva.**

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

Each break was caught by at least two different test levels, which is what the pyramid is meant to give.

---

## Prompt 3: typed API client tests (fake HttpMessageHandler)

**Prompt**

> Write xUnit tests for `ApexApiClient.UploadScansAsync` using a fake `HttpMessageHandler` (no real network). Cover: 200 with JSON, route and camelCase body, special characters in the tracking number, empty body, malformed JSON, unknown JSON fields, 404 with ProblemDetails, 500 with plain text, a theory of error status codes, a 400 validation problem, HttpClient timeout, network failure and caller cancellation.

**What the assistant produced:** `ApexApiClientTests` (14 methods, 19 cases) and `StubHttpMessageHandler`.

**Kept:** the status-code theory (one method, six cases); the test that the tracking number is URL-escaped; the cancellation test where the handler waits on the caller token.

**Changed:** the timeout test originally also expected a retry; retry lives in the Polly handler pipeline, not in `ApexApiClient`, so it now asserts that `TaskCanceledException` (with `TimeoutException` inside) propagates to the resilience layer.

**Rejected:** a test with a real `localhost` server (breaks the no-network rule); asserting log output (couples the test to wording).

**What the fake does not prove:** that the real Polly pipeline retries or opens the circuit; that is shown in the Week 4 runtime evidence, not in unit tests.
