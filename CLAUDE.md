# Repository guidance

## Project shape

- The solution targets .NET 10 and C# 14 with nullable reference types and warnings treated as errors.
- Keep dependency direction: Domain has no project/framework dependencies; Application depends on Domain and defines ports; Infrastructure implements persistence ports; Web is the composition root.
- Keep EF Core, SQL mappings, and query translation in `src/ApexPost.Infrastructure/`. Do not return `IQueryable` from Infrastructure.
- The Web runtime is SQL Server-backed. Do not add JSON fallback behavior to Web startup; JSON adapters are for explicit import flows and tests.
- Couchbase, `ApexPost.CounterTerminal`, offline mode, synchronization, SyncInbox/outbox, and conflict resolution are outside this repository task's scope.

## Persistence changes

- Add schema changes as EF Core migrations under `src/ApexPost.Infrastructure/Persistence/Migrations/`.
- Update standalone SQL reference files under `database/` when their procedure or schema definition changes; regenerate the idempotent deployment script as needed.
- Do not apply migrations automatically at Web startup or use `EnsureCreated`.
- Use parameterized EF/SQL APIs. Keep multi-row business writes within the existing `IUnitOfWork` transaction boundary.
- Keep SQL integration tests isolated: `APEXPOST_SQL_TEST_CONNECTION` must use a catalog prefix of `ApexPost_SqlIT_`. Never point tests at `ApexPostDb`.
- Do not add credentials, connection strings, user-secrets, production data, or sensitive identifiers to source control.

## Application and MVC conventions

- Put business rules in Domain or Application services, not controllers or EF entities.
- Validate user input at the MVC/application boundary and preserve role and branch authorization.
- Return focused report DTOs from Infrastructure and map them into view models in Web.
- Preserve request cancellation through async APIs except for deliberate cleanup/compensation after a write has started.
- Treat optimistic concurrency as a conflict, not as ordinary invalid input.

## Validation commands

```powershell
dotnet build ApexPost.sln --configuration Release
dotnet test ApexPost.sln --configuration Release
```

For SQL integration coverage, configure `APEXPOST_SQL_TEST_CONNECTION` to an isolated LocalDB catalog beginning with `ApexPost_SqlIT_` before running the solution tests. See [the testing guide](docs/WEEK3-TESTING-AND-EXECUTION-GUIDE.md) and [database README](database/README.md) for setup details.
