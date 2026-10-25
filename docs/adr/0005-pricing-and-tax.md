# ADR-0005: Use explicit pricing strategies and tax rules

- **Status:** Accepted
- **Context:** Shipping prices vary by weight, destination zone and service level; retail items have tax categories.
- **Decision:** Load destination/service shipping prices and tax rates from configuration into validated rule objects. `ShippingService` selects the exact destination/service rule and enforces the configured weight limit; transaction line items retain their tax category and tax-rate snapshot. `Money` centralizes two-decimal rounding.
- **Consequences:** Controllers do not calculate prices. Domain/Application tests assert exact totals, and tariff/tax configuration can change without moving pricing rules into MVC.
