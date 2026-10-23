# ADR-0004: Represent mixed baskets using composition

- **Status:** Proposed
- **Context:** One transaction can contain retail goods and services with different pricing and fulfillment rules.
- **Decision:** Model transaction line items through composition and a common line-item abstraction/value contract rather than making all business concepts inherit from one deep class hierarchy.
- **Consequences:** A basket can contain product and service lines while each type retains its own domain-specific data and pricing behavior. Serialization shape must be explicit.
