@api
Feature: Heavy parcel surcharge
  Parcels over 30 kg need extra handling, so they pay a surcharge:
  12.50 plus 0.75 for every kilogram above 30. A parcel of exactly 30 kg pays nothing.

  Background:
    Given the ApexPost API is running with an empty database
    And I am signed in as an administrator

  Scenario Outline: The surcharge depends on the weight
    When I register shipment "APX-20250101" weighing <weight> kg for "Jane Example" in "Springfield"
    Then the API answers with status 201
    And the surcharge shown is <surcharge>

    Examples: light parcels pay nothing
      | weight | surcharge |
      | 0.1    | 0         |
      | 2.5    | 0         |
      | 30     | 0         |

    Examples: heavy parcels pay a surcharge
      | weight | surcharge |
      | 30.01  | 12.51     |
      | 31     | 13.25     |
      | 40     | 20        |
      | 70     | 42.5      |

  Scenario: The surcharge is still shown when the shipment is read again
    Given shipment "APX-20250101" weighing 40 kg is already registered
    And I am signed in as a courier
    When I read shipment "APX-20250101"
    Then the API answers with status 200
    And the surcharge shown is 20

  Scenario: A parcel above the maximum weight is refused instead of being charged
    When I register shipment "APX-20250101" weighing 70.5 kg for "Jane Example" in "Springfield"
    Then the API answers with status 400
    And the validation errors mention "weightKg"
    And no shipments exist
