@service @moneyorders
Feature: Money order insertion
  A counter clerk issues money orders. A small order is stored straight away.
  An order above the verification limit may only be issued when the customer's identity has been verified.
  A refused order must leave nothing behind in the store.

  Background:
    Given the identity verification limit is 1000 rupees
    And the clerk works at branch "IND01" on transaction "TXN-1001"

  Scenario: An order below the limit is inserted without an identity check
    When the clerk issues money order "MO-1001" for 500 rupees
    Then money order "MO-1001" is stored with amount 500 rupees
    And the stored order is stamped with branch "IND01" and transaction "TXN-1001"
    And the order was saved exactly once

  Scenario Outline: Amounts up to the limit never need an identity check
    When the clerk issues money order "MO-2001" for <amount> rupees
    Then money order "MO-2001" is stored with amount <amount> rupees

    Examples:
      | amount |
      | 0.01   |
      | 999.99 |
      | 1000   |

  Scenario Outline: Amounts above the limit are refused without an identity check
    When the clerk issues money order "MO-2002" for <amount> rupees
    Then the issue is refused because identity verification is required
    And no money order is stored

    Examples:
      | amount  |
      | 1000.01 |
      | 1500    |
      | 50000   |

  Scenario: An order above the limit is refused when the identity check failed
    When the clerk issues money order "MO-3001" for 1500 rupees after a failed identity check
    Then the issue is refused because identity verification is required
    And no money order is stored
    And the order was never saved

  Scenario: An order above the limit is inserted when the identity was verified
    When the clerk issues money order "MO-3002" for 1500 rupees after a verified identity check on document "XXXX4321"
    Then money order "MO-3002" is stored with amount 1500 rupees
    And the stored order keeps the masked document number "XXXX4321" marked as verified

  Scenario: A raised limit lets a larger order through without a check
    Given the identity verification limit is 5000 rupees
    When the clerk issues money order "MO-3003" for 4000 rupees
    Then money order "MO-3003" is stored with amount 4000 rupees

  Scenario Outline: Amounts that are not positive are refused
    When the clerk issues money order "MO-4001" for <amount> rupees
    Then the issue is refused because the amount must be greater than zero
    And no money order is stored

    Examples:
      | amount |
      | 0      |
      | -50    |
