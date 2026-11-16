@service @inventory
Feature: Inventory stock management
  Each branch keeps a quantity of every item it sells. Staff look up stock and adjust it when goods arrive or leave.
  Stock can never go below zero, and the branch is warned when an item runs low (5 or fewer left).

  Background:
    Given branch "IND01" holds 20 of item "STAMP01"

  Scenario: A stock lookup shows the quantity held
    When the clerk looks up item "STAMP01" at branch "IND01"
    Then the quantity shown is 20

  Scenario: An item the branch does not hold has no stock record
    When the clerk looks up item "NOSUCH01" at branch "IND01"
    Then no stock record is shown

  Scenario: Adding stock increases the quantity
    When stock of "STAMP01" at "IND01" is adjusted by 5
    Then the quantity of "STAMP01" at "IND01" is 25
    And no low-stock alert was raised

  Scenario: Removing stock decreases the quantity
    When stock of "STAMP01" at "IND01" is adjusted by -8
    Then the quantity of "STAMP01" at "IND01" is 12

  Scenario: Adding a new item creates its stock record
    When stock of "ENVELOPE01" at "IND01" is adjusted by 10
    Then the quantity of "ENVELOPE01" at "IND01" is 10

  Scenario: Stock cannot be removed below zero
    When stock of "STAMP01" at "IND01" is adjusted by -25
    Then the adjustment is refused because there is not enough stock
    And the quantity of "STAMP01" at "IND01" is 20

  Scenario Outline: A low-stock alert is raised when 5 or fewer are left
    When stock of "STAMP01" at "IND01" is adjusted by <change>
    Then the quantity of "STAMP01" at "IND01" is <left>
    And <alerts> low-stock alert was raised

    Examples:
      | change | left | alerts |
      | -14    | 6    | 0      |
      | -15    | 5    | 1      |
      | -20    | 0    | 1      |

  Scenario: An adjustment of zero is refused
    When stock of "STAMP01" at "IND01" is adjusted by 0
    Then the adjustment is refused as invalid
    And the quantity of "STAMP01" at "IND01" is 20

  Scenario: An adjustment needs a reason
    When stock of "STAMP01" at "IND01" is adjusted by 5 without a reason
    Then the adjustment is refused as invalid
    And the quantity of "STAMP01" at "IND01" is 20

  Scenario: An item code with illegal characters is refused
    When the clerk looks up item "BAD CODE!" at branch "IND01"
    Then the lookup is refused as invalid
