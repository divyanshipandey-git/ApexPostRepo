@service @login
Feature: Staff sign-in (credentials verification)
  Staff sign in with an email address and a password. Only an active staff member with the right password gets in.
  Every other attempt is refused in the same way, so a visitor cannot learn which emails exist.
  The test passwords below are made-up values used only in these scenarios.

  Background:
    Given staff member "asha@apexpost.test" is registered at branch "IND01" as "CounterClerk" with password "Test-Only-Pass-1!"

  Scenario: Correct email and password sign the staff member in
    When someone signs in with email "asha@apexpost.test" and password "Test-Only-Pass-1!"
    Then sign-in succeeds as "CounterClerk" at branch "IND01"

  Scenario: The email is matched without regard to case or surrounding spaces
    When someone signs in with email "  ASHA@ApexPost.Test " and password "Test-Only-Pass-1!"
    Then sign-in succeeds as "CounterClerk" at branch "IND01"

  Scenario Outline: Wrong credentials are refused
    When someone signs in with email "<email>" and password "<password>"
    Then sign-in is refused

    Examples:
      | email               | password           |
      | asha@apexpost.test  | Wrong-Pass-9!      |
      | asha@apexpost.test  | test-only-pass-1!  |
      | asha@apexpost.test  |                    |
      | nobody@apexpost.test| Test-Only-Pass-1!  |
      |                     | Test-Only-Pass-1!  |

  Scenario: A deactivated staff member is refused even with the right password
    Given staff member "asha@apexpost.test" is deactivated
    When someone signs in with email "asha@apexpost.test" and password "Test-Only-Pass-1!"
    Then sign-in is refused

  Scenario: Two staff members each sign in with their own password only
    Given staff member "ravi@apexpost.test" is registered at branch "BLR02" as "BranchManager" with password "Test-Only-Pass-2!"
    When someone signs in with email "ravi@apexpost.test" and password "Test-Only-Pass-1!"
    Then sign-in is refused
    When someone signs in with email "ravi@apexpost.test" and password "Test-Only-Pass-2!"
    Then sign-in succeeds as "BranchManager" at branch "BLR02"

  Scenario: Passwords are stored as salted hashes, never as plain text
    Then the stored credential of "asha@apexpost.test" is a salted hash that does not contain "Test-Only-Pass-1!"
