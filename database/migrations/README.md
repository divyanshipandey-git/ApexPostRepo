# EF migration ownership

The migration source remains in [`src/ApexPost.Infrastructure/Persistence/Migrations`](../../src/ApexPost.Infrastructure/Persistence/Migrations). Keep the C# migration classes and model snapshot there so EF Core tooling and migration discovery remain unchanged.

Generate and review the idempotent deployment script from the repository root:

```powershell
$env:APEXPOST_SQL_CONNECTION = '<design-time connection string>'
dotnet ef migrations script --idempotent --project src/ApexPost.Infrastructure --startup-project src/ApexPost.Web --output database/schema/ApexPostDb-idempotent.sql
```

Apply migrations explicitly with `dotnet ef database update`. Web startup is not a migration mechanism. Procedure changes are versioned in migrations; corresponding standalone SQL files under `database/stored-procedures/` are review/deployment references and must be kept synchronized with the latest migration.
