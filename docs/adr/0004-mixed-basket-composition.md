# ADR-0004: Represent mixed baskets using composition

- **Status:** Accepted
- **Context:** One transaction can contain retail goods and services with different pricing and fulfillment rules.
- **Decision:** Model a transaction as a collection of composed `TransactionLineItem` values with a retail/service discriminator. Keep shipment and money-order domain records separate and link them to the transaction by identifier.
- **Consequences:** A basket can contain goods and services without a deep inheritance tree. Persisted line items have one explicit JSON shape; product/shipping/money-order policy remains in its owning service/domain type.
