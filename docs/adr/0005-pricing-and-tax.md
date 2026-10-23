# ADR-0005: Use explicit pricing strategies and tax rules

- **Status:** Proposed
- **Context:** Shipping prices vary by weight, destination zone and service level; retail items have tax categories.
- **Decision:** Use a shipping pricing strategy/rule abstraction and explicit tax calculation rules. Pattern matching and delegates may be used where they make the rule selection clearer; money arithmetic must be centralized and rounded consistently.
- **Consequences:** Pricing can be unit-tested with exact expected values and extended without placing pricing logic in controllers.
