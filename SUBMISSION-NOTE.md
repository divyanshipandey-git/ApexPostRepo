# Week 5 submission note

* **Repository:** https://github.com/divyanshipandey-git/ApexPostRepo (submit the link, not only a zip: the zip has no git history, and the `RED` / `GREEN` / `REFACTOR` commits are part of the grading).
* **Branch name:** the default branch of this repository is **`master`**; the lab text says `main`. `week5-testing` was merged by pull request #12, the completion work by #13, #14 and #15, and the Copilot work was committed directly to `master`.
* **TDD history:** `git log --oneline --grep="^RED\|^GREEN\|^REFACTOR"` (three cycles, see TDD-LOG.md).
* **Run everything:** `dotnet test ApexPost.sln` (983 passed, 27 skipped, 0 failed; with LocalDB enabled 1010 passed, 0 skipped).
* **Evidence:** `docs/evidence/` (raw run logs, `test-run-summary.svg`, coverage summary, eleven deliberate-breakage runs).
* **AI tools used, stated openly:** GitHub Copilot drafted three files (service unit tests, the ScanSyncCopilot feature with its steps, the HTTP client tests); another AI coding assistant made the earlier drafts. Both are logged in COPILOT-LOG.md and every Copilot file carries `COPILOT-ASSISTED`.
* **Strategy diagrams:** `docs/TESTING-DIAGRAMS.md`.
