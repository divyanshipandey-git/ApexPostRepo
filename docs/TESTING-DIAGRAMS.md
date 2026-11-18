# ApexPost testing diagrams

How the testing techniques and tools fit together. The diagrams are Mermaid, so GitHub renders them. A standalone picture of the overview is in [testing-strategy-diagram.svg](testing-strategy-diagram.svg).

## 1. Strategy overview: which technique and tool at which level

```mermaid
flowchart TB
    R["Risk map (9 risks)<br/>status transitions, duplicate tracking numbers, scan de-duplication,<br/>JWT roles, client retry, outbox, validation, queries, surcharge"]

    subgraph BDD["BDD / acceptance: about 7 percent (71 scenarios)"]
        direction LR
        F["Gherkin .feature files<br/>Background, Scenario Outline"] --> RQ["Reqnroll<br/>generates xUnit tests<br/>step bindings, context injection, hooks"]
    end

    subgraph INT["Integration: about 12 percent (115 tests)"]
        direction LR
        WAF["WebApplicationFactory<br/>real API pipeline + real JWT validation"] --- SQ["EF Core on in-memory SQLite<br/>real repositories"]
        CB["Couchbase Lite temp folder<br/>real SQL++ and outbox"]
    end

    subgraph UNIT["Unit: about 81 percent (797 tests), driven by TDD"]
        direction LR
        X["xUnit<br/>Fact, Theory, InlineData, MemberData"] --- M["Moq<br/>Setup, Returns, Verify, Times, It.Is"]
        M --- H["Fake HttpMessageHandler<br/>ApexApiClient: 200, 404, 500, timeout, cancel"]
        H --- T["ManualTimeProvider<br/>no DateTime.Now"]
    end

    R --> BDD
    R --> INT
    R --> UNIT
    BDD -. "API level: ShipmentCreation<br/>service level: ScanSync" .-> INT
    INT -. "runs on" .-> X
    BDD -. "runs on" .-> X
```

## 2. TDD cycle (task 4, heavy parcel surcharge)

```mermaid
flowchart LR
    RED1["RED<br/>failing tests, stub throws<br/>commit: RED: zero up to 30 kg"] --> GREEN1["GREEN<br/>minimal code: return 0<br/>commit: GREEN: minimal"]
    GREEN1 --> RED2["RED<br/>over 30 kg: 12.50 + 0.75 per kg<br/>commit: RED: surcharge"]
    RED2 --> GREEN2["GREEN<br/>smallest passing change<br/>commit: GREEN: surcharge"]
    GREEN2 --> REF["REFACTOR<br/>constants, one max weight, docs<br/>tests unchanged and green<br/>commit: REFACTOR"]
    REF -. "next rule" .-> RED1
```

## 3. BDD flow with Reqnroll

```mermaid
flowchart LR
    A["ScanSync.feature<br/>Given / When / Then<br/>business language"] --> B["Reqnroll code generation<br/>one xUnit test per scenario<br/>and per Examples row"]
    B --> C["Step definitions<br/>ScanSyncSteps"]
    C <--> D["Scenario context<br/>injected into the constructor<br/>(no static fields)"]
    E["Hooks<br/>BeforeScenario: start host<br/>AfterScenario: dispose DB"] --> D
    C --> F{"Level"}
    F -- "service level" --> G["ScanUploadService + SQLite<br/>OutboxService + fake uploader"]
    F -- "API level" --> H["WebApplicationFactory<br/>HTTP, JWT, validation"]
```

## 4. Which data access approach proves what

```mermaid
flowchart TB
    Q{"What are you checking?"}
    Q -- "service decisions and call order" --> MOCK["Mocked repository (Moq)<br/>proves: logic and sequencing<br/>does NOT prove: real query or unique index"]
    Q -- "queries, paging, ordering, keys" --> SQLITE["SQLite in-memory (real EF Core)<br/>proves: LINQ translates, rows come back right<br/>does NOT prove: SQL Server rowversion, datetime2, locking"]
    Q -- "migrations, concurrency, constraints" --> SQL["SQL Server / LocalDB (opt-in)<br/>proves: production provider behaviour<br/>needs a SQL instance, skipped by default"]
    Q -- "offline store" --> CBL["Couchbase Lite temp folder<br/>proves: SQL++, indexes, outbox on the real engine"]
    NO["Never: mock DbContext / DbSet for query logic<br/>LINQ-to-Objects passes where SQL would fail"] -. avoid .-> Q
```
