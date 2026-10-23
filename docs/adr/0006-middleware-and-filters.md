# ADR-0006: Separate HTTP middleware from MVC action filters

- **Status:** Proposed
- **Context:** The assignment requires request-wide security/observability and action-specific auditing/validation/performance behavior.
- **Decision:** Use middleware for HTTP pipeline concerns and filters for MVC action-context concerns. Document exact pipeline ordering and endpoint metadata requirements in DESIGN.md.
- **Consequences:** Responsibilities remain clear. Integration tests must verify externally observable behavior, including correlation headers, security headers, API-key failures and authorization.
