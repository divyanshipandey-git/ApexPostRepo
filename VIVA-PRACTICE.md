# Viva practice: say it out loud, in your own words

Goal: 5 minutes, four topics the lab names. Speak slowly (about 130 words a minute is a comfortable pace). Change any sentence that does not sound like you; the examiner can tell when it is memorised.

Have open: `TESTING-STRATEGY.md`, the GitHub commit list (RED / GREEN / REFACTOR), `docs/evidence/`, a terminal in the project folder.

## The 5-minute script (about 500 words, 4 to 4.5 minutes spoken; the rest is pauses and showing the screen)

**1. Pyramid and why (1 minute 15)**
"ApexPost tracks parcels. Before changing it, the lead wanted proof it is safe, so I made a test strategy first. I listed nine risks, for example status transitions, duplicate tracking numbers, duplicate scans, JWT roles and the client retrying on a bad network. Then I chose a level for each. About 83 percent are unit tests because rules are cheap to test and a failure points at one line. About 13 percent are integration tests with real parts: the real API with real login, and real database code on SQLite. About 4 percent are BDD scenarios in business language. The whole suite is 896 passing tests, and 923 when I switch on SQL Server."

**2. Framework (45 seconds)**
"I used xUnit everywhere. The old tests already used it, so there was no rewrite. xUnit makes a new object for every test, so tests cannot share data by accident, which is my determinism rule. Theory and InlineData give me parameterised tests like the six by six status table. Reqnroll, which runs my BDD, generates xUnit tests, so Test Explorer and dotnet test see everything. One framework, several techniques: TDD, BDD and mocking."

**3. One mock and what it does not prove (1 minute)**
"I mocked the repository and the unit of work to test the scan upload service. The mock proves the service skips a scan that is already stored and never calls Save. It does not prove that the real database rejects a duplicate id, because the mock only does what I told it. So I also test the real repository on SQLite, the real API end to end, and one opt-in test on real SQL Server. I do not mock the database context for queries, because the mock runs the query in memory and would pass where real SQL fails. That is false confidence."

**4. One Copilot test I rejected (1 minute)**
"Copilot drafted a Gherkin feature that was good, but the project file excluded it from the build. The suite was green, yet that feature never ran. A test that does not run proves nothing, so I removed the exclusion and four more scenarios now run. Copilot also used the words empty and missing in the Examples table for no signature; I replaced them with an empty cell. And its client tests only checked responses, so I added a test for the request route. When I changed the route to version one, my test failed. I also say honestly that Copilot saw my earlier drafts, so two of its files overlapped mine."

**5. TDD and proof (45 seconds)**
"For TDD I built a surcharge rule for parcels over thirty kilograms: first a failing test, then the smallest code, then a refactor. You can see RED, GREEN and REFACTOR commits. To prove tests can fail I broke the code on purpose, watched the tests fail, and restored it. For example I disabled the duplicate scan check and three mock tests and two scenarios failed."

## Likely questions, short answers

| Question | Answer |
|---|---|
| Why not mock `DbContext`? | The mock runs LINQ on objects, not SQL, so it passes where real SQL fails: false confidence. |
| What does SQLite not prove? | SQL Server details: rowversion, datetime2, locking, error codes. I keep opt-in SQL Server tests for those; with LocalDB all 923 pass. |
| TDD vs BDD? | TDD is how I write a rule (test first, developer level). BDD describes behaviour in business words that a non-programmer can check. |
| Why is BDD only 4 percent? | It is the slowest and most brittle layer; it documents behaviour, it does not re-test every boundary. |
| What did your tests catch? | Weights from 0.01 to 0.09 kg were accepted; a tracking number ending in a newline passed; the clock could not be tested; two tests failed on the UTC build server because they used the local time zone. |
| Which test gave the most value per minute? | Parameterised unit tests with Moq. |
| Did Copilot write everything? | No. Copilot drafted three files; I changed or extended each one. another AI coding assistant made earlier drafts. Both are logged. |
| What would you change? | Add SQL Server to CI, raise coverage of the console app (about 6 percent now), and keep coverage as evidence, not a goal. |
| How do you run it? | `dotnet test ApexPost.sln`, no setup. |
| Show a test failing. | Change `<=` to `<` in `HeavyParcelSurcharge`, run the surcharge tests, see them fail, then `git checkout -- <file>`. |

## Rehearsal checklist
- [ ] Read the script out loud once with a timer; it should land between 4:00 and 5:00.
- [ ] Rewrite two sentences in your own words.
- [ ] Open `COPILOT-LOG.md` and read the Kept / Changed / Rejected tables once; pick the one rejected case you will tell.
- [ ] Practise the live breakage demo once so the commands are not new on the day.
- [ ] Be ready to say "I do not know, but I would check X" instead of guessing.
