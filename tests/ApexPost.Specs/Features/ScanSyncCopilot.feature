# COPILOT-ASSISTED: drafted with GitHub Copilot, reviewed and changed by the student; see COPILOT-LOG.md (prompt 2)
@service
Feature: Courier scan upload sync
  Courier devices can record scans while offline and later upload them to the service.
  The service accepts valid scans once, skips duplicates, and rejects invalid scans with a clear reason.

  Background:
	Given a shipment "APX-20250101" is ready for courier upload at "2025-03-10 08:00"
	And the service clock is set to "2025-03-10 12:00"
	And the courier performing the upload is "COURIER-001"

  Scenario: Offline scans are uploaded
	Given the device has 2 offline scans queued for upload
	When the courier uploads the queued scans for "APX-20250101"
	Then the upload accepts 2 scans
	And the upload skips 0 duplicates
	And the shipment stores 2 scans

  Scenario: A retried upload does not create duplicates
	Given the device has 2 offline scans queued for upload
	And the same scans were already uploaded for "APX-20250101"
	When the courier uploads the queued scans for "APX-20250101"
	Then the upload accepts 0 scans
	And the upload skips 2 duplicates
	And the shipment stores 2 scans

  Scenario Outline: Invalid scans are rejected with a reason
	Given the device has one invalid scan of type "<scanType>" at <minutes> minutes from now with signature "<signature>"
	When the courier uploads the queued scans for "APX-20250101"
	Then the upload rejects 1 scan with the reason "<reason>"
	And the shipment stores 0 scans

	Examples:
	  | scanType  | minutes | signature | reason                                             |
	  | Pickup    | 30      |            | Scan time cannot be in the future                   |
	  | Delivered | 0       |            | Recipient signature is required for Delivered scans |
