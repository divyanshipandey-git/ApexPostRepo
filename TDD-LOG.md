# TDD Log: heavy parcel surcharge

## 1. The requirement (did not exist before)

> **Shipments over 30 kg require a surcharge.**
> Agreed formula: flat fee **12.50** plus **0.75 per kg above 30 kg**, rounded to cents (away from zero).
> A parcel of exactly 30 kg is **not** "over 30 kg" and pays nothing. Weights outside the accepted range (above 70 kg, or not positive) are an error.

Location: `src/ApexPost.Domain/Pricing/HeavyParcelSurcharge.cs`, tests in `tests/ApexPost.UnitTests/Pricing/HeavyParcelSurchargeTests.cs`.

## 2. Red, Green, Refactor in the Git history

```
git log --oneline --grep="^RED\|^GREEN\|^REFACTOR"
```

| Step | Commit | What the commit contains |
|---|---|---|
| **RED 1** | `4ce93a9` | Tests for "no surcharge up to 30 kg" (incl. exactly 30) and "invalid weights throw". Production file is only a stub that throws `NotImplementedException`, so the solution builds and **7 tests fail**. |
| **GREEN 1** | `92d2734` | Minimal code: range check and `return 0m`. 7 tests pass. Deliberately no formula yet. |
| **RED 2** | `976f9ab` | Tests for the surcharge itself: 30.01 kg gives 12.51 (rounding), 31 gives 13.25, 40 gives 20.00, 70 gives 42.50, and "just over 30 is never cheaper than the flat fee". **5 tests fail**, 7 still pass. |
| **GREEN 2** | `7875f99` | Smallest change that passes: `if (weight > 30) return Math.Round(12.50m + (weight - 30m) * 0.75m, 2, AwayFromZero)`. 12 tests pass. |
| **REFACTOR** | `150fa37` | Named constants (`ThresholdKg`, `FlatFee`, `RatePerKgOverThreshold`), the maximum weight now comes from `TrackedShipment.MaxWeightKg` instead of a second literal `70`, guard clause first, XML documentation, exception message and actual value. **Behaviour unchanged; the 12 tests stay green.** |

## 3. The first failing test

```csharp
[Theory]
[InlineData("0.1")] [InlineData("10")] [InlineData("29.99")]
[InlineData("30")]   // exactly 30 kg is NOT "over 30 kg"
public void Calculate_AtOrBelowThirtyKg_IsZero(string weight) { ... Assert.Equal(0m, surcharge); }
```

It failed with `NotImplementedException`: the stub existed only so the project compiled (a compile error is not a meaningful red). The boundary at exactly 30 kg was written **before** any code, which is the point of test-first: the question "is 30 over 30?" was answered in the test, not discovered later.

## 4. The minimal implementation

GREEN 1 returned `0m` after the range check, which is the simplest thing that makes the first test set pass. GREEN 2 added only the branch that the new failing tests demanded. The rounding was not in my head when I started cycle 2: test `30.01 kg -> 12.51` (the raw result is 12.5075) is what forced `Math.Round`.

## 5. What was refactored

* magic numbers 12.50, 0.75, 30 and 70 became named constants / a single source of truth;
* the guard clause moved first, the happy path last;
* XML docs state the rule in business words ("exactly 30 kg pays nothing");
* tests were not changed in the refactor commit, which is the check that behaviour did not move.

## 6. Production code changed to make it testable

| Change | Why |
|---|---|
| `HeavyParcelSurcharge` is a **static pure function** of one `decimal` | No clock, no I/O, no state: nothing needs mocking, tests are one line each. |
| `TrackedShipment.MinWeightKg` / `MaxWeightKg` public constants | The rule and the tests share one definition of the accepted range (also used by the new boundary tests). |
| `ScanUploadService` takes an **optional `TimeProvider`** (default `TimeProvider.System`) | The service called `DateTimeOffset.UtcNow` for the "scan time cannot be in the future" rule, so a test at the 5-minute boundary could not be deterministic. With an injected clock the boundary tests use a manual clock. No caller had to change (optional constructor parameter). |
| `IShipmentUploader`, `IScanRepository`, `ITrackedShipmentRepository`, `IUnitOfWork` already existed as interfaces | They are what Moq replaces; no extraction was needed. |

## 7. Other defects found by tests (not TDD, but test-driven)

Boundary tests of commit `afa7099` failed on purpose-found gaps in `TrackedShipment.Create`, fixed in `e3d23eb`:

1. weights 0.01 to 0.09 kg were accepted although the rule (and the error message) says the minimum is 0.1 kg;
2. `"APX-20250101\n"` passed `^APX-\d{8}$` because .NET `$` also matches before a final newline; the domain now uses `\z`.

## 8. Cycle 3: the rule is wired into the API

The first two cycles built the pure rule. Cycle 3 applies it to a real shipment and exposes it in the v2 API, again test-first.

| Step | Commit | What happened |
|---|---|---|
| **RED** | `88afae6` | A stub `TrackedShipment.Surcharge` returns 0. New tests fail: 3 unit tests (`TrackedShipmentSurchargeTests`: 40 kg, 70 kg, recalculation after `UpdateDetails`) and 4 API tests (`PostShipment_V2_ReportsTheHeavyParcelSurcharge` through `WebApplicationFactory`; a 5th test, "v1 has no surcharge field", is already green and guards the frozen v1 contract). |
| **GREEN** | `088ae10` | `Surcharge => HeavyParcelSurcharge.Calculate(WeightKg)` and `SurchargeAmount` in `V2ShipmentDetail`, mapped in `MapToV2Detail`. |
| **REFACTOR** | `d6fa243` | `using` instead of a fully qualified name; the DTO documentation points at `HeavyParcelSurcharge` instead of repeating 12.50 / 0.75 / 30 (one source of truth). Behaviour unchanged. |

Follow-up (not a TDD step): `contracts/v2.json` was re-exported from the running API and the NSwag client regenerated (`surchargeAmount` is additive, so non-breaking, see VERSIONING.md). The BDD feature `HeavyParcelSurcharge.feature` describes the same rule in business language.

Design notes: `Surcharge` is a computed, get-only property, so EF Core does not map or store it (nothing to migrate); the surcharge is derived from the weight every time, so it can never disagree with it.

## 9. Still open on purpose

The surcharge is only reported (`surchargeAmount`); nothing charges or stores money yet. Billing needs a product decision (who pays, currency, tax), so it was not invented here.
