# Viva notes: ApexPost testing in 5 minutes

Say it in your own words. Short, simple, true.

## The project in one breath (20 seconds)
ApexPost is a parcel-tracking system: a SQL Server API, an offline courier store (Couchbase Lite) and a courier client. Week 5 asks: **how do we know it is safe to change?** My answer is a deliberate test strategy, not just many tests.

## 1. My test pyramid (1 minute)
- **Unit tests, about 83 %** (718): fast, test one rule. Example: the status matrix (Delivered can never go back).
- **Integration tests, about 13 %** (115): real components. Example: the real API with real JWT login, and real EF Core on in-memory SQLite.
- **BDD, about 4 %** (33 scenarios): business language, checks what the customer sees.
- Why this shape: rules are cheap to test at the bottom; wiring (login, routes, database) can only be tested with real parts, so fewer but bigger tests.
- Result: **866 tests pass, 0 fail**, 27 skipped by default (they need SQL Server; with LocalDB enabled all 893 pass).

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

## 6. One AI test I rejected (30 seconds)
The draft said a tracking number with spaces around it (`"  APX-20250101  "`) was **valid**, because the code calls `Trim()`. That test **copied the implementation**. The rule says the number must match `APX-` plus 8 digits, so I changed it to **invalid**.
(Other real review fixes: a status test with the wrong expected code, and a timeout test that wrongly expected a retry.)
**Be honest about the tool:** I had no GitHub Copilot, so I used another AI coding assistant and said so in `COPILOT-LOG.md`.

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
- **SQL Server:** the unique-tracking-number test runs on real SQL Server (LocalDB) when `APEXPOST_SQL_TEST_CONNECTION` is set; then 893 tests pass with none skipped.
- **Cleanup:** I merged three duplicated Domain test files (330 tests before and after, none lost) and recorded it in the review table.
- **CI:** `.github/workflows/tests.yml` runs `dotnet test` on every push.
- **Default branch is `master`** (the lab text says `main`).
