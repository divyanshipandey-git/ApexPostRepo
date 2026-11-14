# AI-ASSISTED: drafted with an AI assistant (Claude Code), then reviewed; see COPILOT-LOG.md
@service
Feature: Scan synchronisation
  Couriers scan parcels where there is no network. When the phone is online again the scans are uploaded.
  Each scan must be stored exactly once, even if the app retries, and bad scans are rejected with a reason.

  Background:
    Given shipment "APX-20250101" was registered at "2025-03-10 08:00"
    And the time is now "2025-03-10 12:00"
    And I am courier "COURIER-001"

  Scenario: Scans recorded offline are uploaded
    Given I recorded 2 scans while offline
    When I upload my scans for "APX-20250101"
    Then 2 scans are accepted
    And 0 duplicates are skipped
    And the shipment history contains 2 scans

  Scenario: A retried upload does not create duplicates
    Given I recorded 2 scans while offline
    And I have already uploaded my scans for "APX-20250101"
    When I upload my scans for "APX-20250101"
    Then 0 scans are accepted
    And 2 duplicates are skipped
    And the shipment history contains 2 scans

  Scenario: A batch mixing new and already known scans is split correctly
    Given I recorded 3 scans while offline
    And only my first scan has already been uploaded
    When I upload my scans for "APX-20250101"
    Then 2 scans are accepted
    And 1 duplicate is skipped
    And the shipment history contains 3 scans

  Scenario Outline: Invalid scans are rejected with a reason
    Given I recorded a "<type>" scan <minutes> minutes from now with signature "<signature>"
    When I upload my scans for "APX-20250101"
    Then 1 scan is rejected with the reason "<reason>"
    And the shipment history contains 0 scans

    Examples:
      | type      | minutes | signature    | reason                                      |
      | Pickup    | 30      |              | Scan time cannot be in the future           |
      | Delivered | -5      |              | Recipient signature is required             |

  Scenario: A courier cannot upload scans of another courier
    Given I recorded 1 scan while offline
    And one of my scans carries the courier id "COURIER-999"
    When I try to upload my scans for "APX-20250101"
    Then the upload is forbidden
    And the shipment history contains 0 scans

  Scenario: The outbox marks uploaded scans as synced and failed ones as failed
    Given 3 scans are waiting in the offline outbox
    And the server accepts only the first scan
    When the outbox runs
    Then 1 scan is marked Synced
    And 2 scans are marked Failed

  Scenario: Failed scans are retried on the next run
    Given 2 scans are waiting in the offline outbox
    And the server rejects everything
    And the outbox has run once
    And the server is back online
    When the outbox runs
    Then 2 scans are marked Synced
    And 0 scans are marked Failed
