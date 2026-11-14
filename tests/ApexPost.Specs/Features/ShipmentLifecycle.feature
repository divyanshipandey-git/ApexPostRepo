@api
Feature: Shipment lifecycle
  A parcel moves forward through fixed steps: Created, PickedUp, InTransit, OutForDelivery, Delivered
  (or Failed). Couriers change the status; steps cannot be skipped and a finished parcel cannot move back.

  Background:
    Given the ApexPost API is running with an empty database
    And shipment "APX-20250101" is already registered
    And I am signed in as a courier

  Scenario: A parcel travels from pickup to delivery
    When I change the status of "APX-20250101" to "PickedUp"
    And I change the status of "APX-20250101" to "InTransit"
    And I deliver shipment "APX-20250101" signed by "Jane Example"
    Then the API answers with status 204
    And shipment "APX-20250101" can be read back with status "Delivered"

  Scenario: A step cannot be skipped
    When I deliver shipment "APX-20250101" signed by "Jane Example"
    Then the API answers with status 409
    And shipment "APX-20250101" can be read back with status "Created"

  Scenario Outline: A delivered parcel cannot move back
    Given shipment "APX-20250101" has been delivered
    When I change the status of "APX-20250101" to "<earlier status>"
    Then the API answers with status 409
    And shipment "APX-20250101" can be read back with status "Delivered"

    Examples:
      | earlier status |
      | InTransit      |
      | PickedUp       |
      | OutForDelivery |

  Scenario: Only a parcel that has not been picked up can be deleted
    Given shipment "APX-20250101" has been picked up
    And I am signed in as an administrator
    When I delete shipment "APX-20250101"
    Then the API answers with status 409
    And shipment "APX-20250101" can be read back with status "PickedUp"

  Scenario: An untouched parcel can be deleted by an administrator
    Given I am signed in as an administrator
    When I delete shipment "APX-20250101"
    Then the API answers with status 204
    And no shipments exist
