# Week 3 Interview and Viva Questions

## 1. Why SQL Server instead of JSON persistence?

Answer:

The Week 3 assignment requires replacing the JSON-only persistence with a system-of-record relational database because the business works with branch-level operations, transaction integrity, concurrency, reports, and multi-row relational logic that JSON files cannot support reliably at scale. SQL Server gives transactional correctness, indexing, reporting, and stored procedures as a proper storage boundary while keeping the application-level interfaces unchanged.

Evidence in the code:

- `src/ApexPost.Infrastructure/Persistence/SqlInfrastructureExtensions.cs`
- `src/ApexPost.Infrastructure/Persistence/SqlUnitOfWork.cs`
- `src/ApexPost.Infrastructure/Persistence/ApexPostDbContext.cs`

## 2. Code First versus Database First

Answer:

This implementation uses Code First. The schema is modeled in `ApexPostDbContext` and entity configuration classes, and EF Core migrations generate and track schema evolution in source control.

This gives better version control, reviewability, and deployment discipline than database-first generation because the schema changes are checked into the repository and can be reported to trainers and deployment reviewers.

Evidence:

- `src/ApexPost.Infrastructure/Persistence/ApexPostDbContext.cs`
- `src/ApexPost.Infrastructure/Persistence/Migrations/*`

## 3. What is the purpose of EF Core `DbContext`?

Answer:

`DbContext` manages the runtime model, database connection, change tracking, and query execution. `ApexPostDbContext` centralizes entity mapping and exposes the relational tables used by the application. It is also the gateway to EF Core migrations and query execution.

## 4. What is the repository pattern and why is it useful here?

Answer:

The repository pattern abstracts persistence behind an application-level contract so the domain and services do not depend on SQL or file-system details. The application depends on interfaces such as `IBranchRepository` and `IInventoryRepository`, while the infrastructure implements the SQL-specific repository classes.

This keeps the Week 2 business logic and application service layer stable while the database implementation changes underneath it.

## 5. What is Unit of Work and why does it matter?

Answer:

The Unit of Work pattern ensures that a related group of domain changes is persisted together through a single commit boundary. The `SqlUnitOfWork` implementation centralizes `SaveChangesAsync()` and implements transaction support for multi-step operations.

This matters for stock adjustments, counter sales, money-order flows, and other transactional business use cases where multiple tables must succeed together.

## 6. Why are dependency injection and scoped lifetime important?

Answer:

The container ensures there is a single shared `ApexPostDbContext` per request scope, while repositories and the unit of work are resolved from the same shared dependency graph. This keeps the data access state consistent and prevents cross-request contamination.

The result is a clean runtime model for request processing and transactional persistence.

## 7. What is the difference between eager, explicit, and lazy loading in this project?

Answer:

The implementation generally avoids lazy loading and relies on explicit query composition. Features such as `Include()` and `AsNoTracking()` are used where the retrieval must materialize related rows without unnecessary tracking. This avoids implicit database calls and keeps queries predictable and measurable. The repository layer is intentionally explicit rather than relying on hidden, late-bound database access.

## 8. What is the purpose of the stored procedures here?

Answer:

Stored procedures are used for server-side operations that benefit from transactional validation, centralization, and direct SQL logic, such as stock adjustments and the reporting-driven aggregation flows. They are created with `CREATE OR ALTER` and parameterized invocation from the application, not by string concatenation.

## 9. How is concurrency handled?

Answer:

Concurrency is handled with optimistic concurrency patterns, database execution strategies, and explicit transaction boundaries. `DbUpdateConcurrencyException` is translated into a safe application-level conflict error so that retries or user-visible conflict messaging can occur without corrupting data.

## 10. How is SQL-only runtime validation enforced?

Answer:

The runtime fails if the SQL connection string is missing and uses `AddSqlInfrastructure` in the web startup path. This ensures the application can only operate with the SQL configuration rather than silently falling back to another persistence layer.

## 11. What is the import safeguard?

Answer:

The JSON import process generates a fingerprint of the data directory and records the import in `ImportRuns`. The importer refuses to re-import duplicate source content and refuses to insert into an already-populated business database without a matching import record. This preserves data integrity and prevents accidental duplication.

## 12. Why is the design not a raw JSON fallback at runtime?

Answer:

This project’s runtime model is not only database-backed but also validated in tests and production startup. The `AddSqlInfrastructure` method is the runtime registration. The application reads from SQL Server and the SQL repository implementations for business access. The JSON data importer is a migration utility, not the operating persistence model.

## 13. What is the role of the SQL performance evidence?

Answer:

The performance evidence is retained in the repository to document actual SQL Server LocalDB results for real workloads, without claiming production capacity. The project includes results for low stock, daily branch sales, shipping queries, and money-order queries, and the results are preserved in the repository files rather than discarded.

## 14. What remains outside scope and why?

Answer:

The excluded offline-first and Couchbase features are intentionally not part of this submission because the assignment scope and Week 2 requirements require SQL persistence and/or server-side relational database work first. The repository remains faithful to the instructed boundaries and avoids introducing out-of-scope functionality that would dilute a clean Week 3 implementation.

## 15. Final viva summary

The Week 3 implementation is successful because it preserves the original business model, moves the runtime to a proper SQL Server system of record, keeps repository abstraction boundaries intact, validates the Work through real integration tests, and documents the architecture and measured evidence in the repository.
