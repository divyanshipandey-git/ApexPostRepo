# Architecture decision records

## Current SQL/application decisions

- [ADR-0015 — SQL Server runtime](0015-sql-runtime-and-reporting-boundary.md)
- [ADR-0016 — Relational schema and delete behavior](0016-relational-schema-and-delete-behavior.md)
- [ADR-0017 — Evidence-based indexes](0017-indexes-and-measured-workloads.md)
- [ADR-0018 — Stored procedure boundaries](0018-stored-procedure-boundaries.md)
- [ADR-0019 — Repository and Unit of Work](0019-repository-and-unit-of-work-boundaries.md)
- [ADR-0020 — Domain and persistence models](0020-domain-and-persistence-model-separation.md)
- [ADR-0021 — Transactions and concurrency](0021-transactions-and-optimistic-concurrency.md)
- [ADR-0022 — Audit persistence isolation](0022-audit-persistence-isolation.md)
- [ADR-0023 — Report DTOs and input mapping](0023-report-dtos-and-input-mapping.md)
- [ADR-0024 — SQL parameterization and permissions](0024-sql-parameterization-and-runtime-permissions.md)
- [ADR-0025 — Patched PKCS dependency override](0025-pkcs-security-override.md)

ADR-0003, ADR-0008, ADR-0009, and ADR-0014 record historical JSON behavior and are superseded for the Web runtime by ADR-0015. The remaining Week 2 decisions continue to describe the business/domain and Web behavior where they do not conflict with SQL persistence. This repository does not adopt Couchbase or offline synchronization.
