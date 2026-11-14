@api
Feature: Shipment creation
  Counter administrators register parcels so that couriers can later scan them.
  A tracking number identifies exactly one parcel, and bad input must be refused with a clear reason.

  Background:
    Given the ApexPost API is running with an empty database
    And I am signed in as an administrator

  Scenario: Registering a valid shipment
    When I register shipment "APX-20250101" weighing 2.5 kg for "Jane Example" in "Springfield"
    Then the API answers with status 201
    And the Location header ends with "/api/v2/shipments/APX-20250101"
    And shipment "APX-20250101" can be read back with status "Created"

  Scenario: A tracking number cannot be used twice
    Given shipment "APX-20250101" is already registered
    When I register shipment "APX-20250101" weighing 1 kg for "John Sample" in "Shelbyville"
    Then the API answers with status 409
    And the problem title is "Conflict"
    And only one shipment exists in total

  Scenario Outline: Invalid shipment details are refused
    When I register shipment "<tracking>" weighing <weight> kg for "Jane Example" in "Springfield"
    Then the API answers with status 400
    And the validation errors mention "<field>"

    Examples: bad weights
      | tracking     | weight | field          |
      | APX-20250101 | 0.05   | weightKg       |
      | APX-20250101 | 70.5   | weightKg       |
      | APX-20250101 | 0      | weightKg       |

    Examples: bad tracking numbers
      | tracking     | weight | field          |
      | APX-1234     | 2.5    | trackingNumber |
      | apx-20250101 | 2.5    | trackingNumber |
      | 20250101     | 2.5    | trackingNumber |

  Scenario: A courier is not allowed to register shipments
    Given I am signed in as a courier
    When I register shipment "APX-20250101" weighing 2.5 kg for "Jane Example" in "Springfield"
    Then the API answers with status 403
    And no shipments exist
