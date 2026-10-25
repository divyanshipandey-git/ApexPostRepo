# ADR-0010: Record full transaction refunds without simulating payment reversals

- **Status:** Accepted
- **Context:** The current transaction model supports a single `Paid` to `Refunded` state transition and has no payment processor, refund ledger, or partial-refund amount model. Voiding and refunding must remain distinct actions.
- **Decision:** Allow only an active Branch Manager for the transaction's current home branch or an active Administrator to refund a Paid transaction. Require a reason, persist the `Refunded` state and reason, publish the existing simulated refund notification, and use the existing global MVC audit filter. The details form names the operation as a full refund and successful posts follow PRG. Reject open, voided, and already-refunded transactions. Do not claim a payment-provider reversal or partial refund.
- **Consequences:** The workflow is auditable and branch-restricted using the same current-staff authorization path as voiding. This is an operational state record only; a later payment integration must add an idempotent financial reversal/ledger contract before real funds can be returned.
