# ADR-0001: Use DDD-inspired Clean Architecture

- **Status:** Accepted
- **Context:** Week 1 TechNova RMS used a layered console-oriented design. Week 2 changes the business domain and adds MVC, middleware, filters, security and audit requirements.
- **Decision:** Create separate Domain, Application, Infrastructure and Web projects. Use DDD patterns where they enforce meaningful business invariants.
- **Consequences:** Domain remains framework-independent. More project boundaries require explicit interfaces and dependency registration, but allow isolated tests and replaceable infrastructure.
- **Alternatives:** Keep the previous layered console structure unchanged; adopt a full CQRS/event-sourcing stack. Both are rejected as poor fits for the scope.
