# Viva notes: ApexPost testing in 5 minutes

Say it in your own words. Short, simple, true.

## The project in one breath (20 seconds)
ApexPost is a parcel-tracking system: a SQL Server API, an offline courier store (Couchbase Lite) and a courier client. Week 5 asks: **how do we know it is safe to change?** My answer is a deliberate test strategy, not just many tests.

## 1. My test pyramid (1 minute)
- **Unit tests, about 83 %** (760): fast, test one rule. Example: the status matrix (Delivered can never go back).
- **Integration tests, about 13 %** (115): real components. Example: the real API with real JWT login, and real EF Core on in-memory SQLite.
- **BDD, about 4 %** (37 scenarios): business language, checks what the customer sees.
- Why this shape: rules are cheap to test at the bottom; wiring (login, routes, database) can only be tested with real parts, so fewer but bigger tests.
- Result: **912 tests pass, 0 fail**, 27 skipped by default (they need SQL Server); with LocalDB enabled **939 pass, 0 skipped**.

## 2. Why xUnit (30 seconds)
- All the earlier tests already used xUnit, so no rewrite.
- A **new object per test**, so tests cannot leak data into each other (that is my "no shared state" rule).
- `[Theory]` gives parameterised tests (tracking number, weight, 6x6 status table).
- **Reqnroll** (BDD) generates xUnit tests, and Moq works with it: **one framework, several techniques**.

## 3. TDD in one example (1 minute)
Rule: **parcels over 30 kg pay a surcharge** (12.50 + 0.75 per kg above 30).
1. **RED:** write a failing test first (exactly 30 kg pays nothing).
2. **GREEN:** the smallest code that passes.
3. **REFACTOR:** clean up with the tests still green.
- Done three times; see `git log --grep="^RED\|^GREEN\|^REFACTOR"` and `TDD-LOG.md`.
- To make code testable I injected a clock (`TimeProvider`) instead of `DateTime.Now`.

## 4. BDD in one example (30 seconds)
`ScanSync.feature` says in plain words: "a retried upload does not create duplicates". Reqnroll turns each Given/When/Then into a test. State is shared by **context injection**, and **hooks** start and clean up the database for every scenario.

## 5. One mock and what it does NOT prove (45 seconds)
I mocked `ITrackedShipmentRepository` and `IUnitOfWork` to test `ScanUploadService`: it proves the service **skips a duplicate and never calls Save**. It does **not** prove the real database rejects a duplicate id. That is why I also test the real repository on SQLite and the real API.
- Same idea: SQLite is not SQL Server (no rowversion, different dates), so I say so in the strategy and keep opt-in SQL Server tests.
- I never mock `DbContext`/`DbSet` for queries: LINQ-to-Objects would pass where real SQL fails (false confidence).

## 6. One Copilot test I rejected (45 seconds)
Pick ONE and tell it in your own words (details in `COPILOT-LOG.md`):
- **The feature that never ran.** Copilot gave me a good `ScanSyncCopilot.feature`, but the project file excluded it from the build, so the suite was green while testing nothing. I removed the exclusion and 4 more scenarios now run. Lesson: a test that is not executed proves nothing.
- **Magic words.** Copilot wrote `empty` and `missing` in the Examples table for "no signature". I rejected that and used an empty cell, because the meaning should be in the scenario, not hidden in the step code.
- **No look at the request.** Copilot's six client tests checked responses only, so a client posting to the wrong route would still pass. I rejected that gap and added a test for the route and JSON names; changing `api/v2` to `api/v1` now fails it.
- **Weak assertion.** For "same scan id twice" Copilot checked only the counts; I added a check that exactly one scan is stored.

Be honest about overlap: Copilot saw my earlier drafts, so prompts 1 and 3 gave almost the same tests. I say that plainly and point to what I added in review.

**Be honest about the tool:** GitHub Copilot drafted three files (service unit tests, the ScanSyncCopilot feature with steps, the HTTP client tests); another AI coding assistant made the earlier drafts. Both are logged in `COPILOT-LOG.md`, every Copilot file carries `COPILOT-ASSISTED`, and I changed or extended each one in review.

## 7. Proof the tests can fail (30 seconds)
I broke the code on purpose and the tests failed, then I restored it:
- disabled the duplicate-scan check: 3 mock tests + 2 BDD scenarios failed;
- allowed Delivered to InTransit: the status matrix failed;
- changed the 30 kg threshold: a unit, an integration and a BDD test failed.

## 8. What the tests caught (30 seconds)
- Weights 0.01 to 0.09 kg were accepted although the rule says minimum 0.1 kg.
- A tracking number ending in a newline passed the format check.
- The clock could not be tested, so I injected one.

## 9. What I would change
Add coverage reports, run the SQL Server tests in CI, and merge two duplicated Domain test files.

## Questions you may get
- **Why not mock the database?** False confidence (see 5).
- **Why is BDD only 4 %?** It is the most expensive layer; it should document behaviour, not re-test every boundary.
- **Which test type gave the most value per minute?** Parameterised unit tests with Moq.
- **How do I run everything?** `dotnet test ApexPost.sln`, no setup.
- **Where is the strategy?** `TESTING-STRATEGY.md` (written first), diagrams in `docs/TESTING-DIAGRAMS.md`.

## Extra facts for the viva (one line each)
- **Coverage:** about 48 % of lines overall; Domain about 69 %. It is evidence of where tests are missing (console glue, Swagger setup), not a goal.
- **SQL Server:** the unique-tracking-number test runs on real SQL Server (LocalDB) when `APEXPOST_SQL_TEST_CONNECTION` is set; then 939 tests pass with none skipped.
- **Cleanup:** I merged three duplicated Domain test files (330 tests before and after, none lost) and recorded it in the review table.
- **CI:** `.github/workflows/tests.yml` runs `dotnet test` on every push.
- **Default branch is `master`** (the lab text says `main`).
