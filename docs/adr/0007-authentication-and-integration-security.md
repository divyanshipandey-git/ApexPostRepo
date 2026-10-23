# ADR-0007: Use cookie authentication and protect partner endpoints explicitly

- **Status:** Proposed
- **Context:** ASP.NET Core Identity is prohibited. The platform needs role/branch authorization and an API-key protected courier integration.
- **Decision:** Implement cookie authentication with StaffId, Role and BranchCode claims, authorization policies, antiforgery, secure cookie settings, configured API keys compared in constant time, IP allowlists and endpoint-specific rate limiting/CORS.
- **Consequences:** Configuration and proxy assumptions must be documented. CORS is not treated as authentication or authorization. Secrets remain outside source control.
