# Migrations

The EF Core migrations are maintained in [`src/ApexPost.Infrastructure/Persistence/Migrations/`](../../src/ApexPost.Infrastructure/Persistence/Migrations/). Do not create a second migration history under this folder. Apply migrations explicitly with `dotnet ef database update`; never auto-migrate at Web startup.
