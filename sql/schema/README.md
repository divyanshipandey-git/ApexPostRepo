# Schema

The authoritative schema is generated from EF Core migrations under [`src/ApexPost.Infrastructure/Persistence/Migrations/`](../../src/ApexPost.Infrastructure/Persistence/Migrations/). The standalone idempotent SQL reference is [`database/schema/ApexPostDb-idempotent.sql`](../../database/schema/ApexPostDb-idempotent.sql). Keep both in sync when a schema migration changes.
