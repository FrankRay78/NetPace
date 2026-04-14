# Feature Specification: Add Hostname and IP Address to Structured Output

**Feature Branch**: `001-hostname-ip-output`  
**Created**: 2026-04-14  
**Status**: Draft  
**Input**: GitHub Issue #141 — Add hostname and IP address to JSON output for NMS integration

## User Scenarios & Testing *(mandatory)*

### User Story 1 - JSON output includes device identifiers (Priority: P1)

As a network engineer integrating NetPace into a Network Management System (NMS) such as NetBox or OpenNMS, I want the JSON output to include the device's hostname and IP address so I can automatically correlate speed test results with existing device records in my monitoring system — without manually enriching the data.

**Why this priority**: NMS correlation is the primary motivation for this feature. JSON is the most commonly consumed format for automated ingestion pipelines. Without device identifiers, NMS integration requires a manual mapping step that defeats the purpose of automation.

**Independent Test**: Can be fully tested by running `netpace --output json` on a machine and verifying that the output JSON contains `IPAddress` and `Hostname` fields at the end of the result object with correct values.

**Acceptance Scenarios**:

**Scenario: JSON output includes IPAddress and Hostname for a machine with IPv4 and hostname**
1. **Given** a machine with a resolvable hostname and at least one active IPv4 network interface, **When** a speed test is run with JSON output, **Then** the JSON result includes an `IPAddress` field containing the first available IPv4 address and a `Hostname` field containing the device hostname, both appearing after the existing fields.

**Scenario: JSON output uses IPv6 when no IPv4 is available**
2. **Given** a machine with no active IPv4 interface but at least one active IPv6 interface, **When** a speed test is run with JSON output, **Then** the `IPAddress` field contains the first available IPv6 address.

**Scenario: JSON output uses empty string when no IP address is available**
3. **Given** a machine with no active network interfaces, **When** a speed test is run with JSON output, **Then** the `IPAddress` field contains an empty string and execution completes normally.

**Scenario: JSON output uses ERROR when IP address retrieval throws**
4. **Given** a condition where fetching the IP address raises an exception, **When** a speed test is run with JSON output, **Then** the `IPAddress` field contains the string `"ERROR"` and no unhandled exception is surfaced to the user.

**Scenario: JSON output uses ERROR when hostname retrieval throws**
5. **Given** a condition where fetching the hostname raises an exception, **When** a speed test is run with JSON output, **Then** the `Hostname` field contains the string `"ERROR"` and no unhandled exception is surfaced to the user.

**Scenario: JSON output uses empty string when hostname resolves to empty**
6. **Given** a machine whose hostname resolves to an empty string, **When** a speed test is run with JSON output, **Then** the `Hostname` field contains an empty string.

---

### User Story 2 - CSV output includes device identifiers (Priority: P2)

As an automation engineer building a pipeline that imports NetPace speed test results into a monitoring dashboard or fleet management system, I want the CSV output to include device hostname and IP address columns so I can join results directly with device inventory without a separate enrichment step.

**Why this priority**: CSV is the second structured output format and is commonly used for batch imports and spreadsheet-based analysis. Device identifiers are equally important for correlation workflows. Lower priority than JSON as JSON is the primary machine-readable format for NMS integration.

**Independent Test**: Can be fully tested by running `netpace --output csv` and confirming the output contains `IPAddress` and `Hostname` columns at the end of each row with correct values.

**Acceptance Scenarios**:

**Scenario: CSV output includes IPAddress and Hostname columns for a machine with IPv4 and hostname**
1. **Given** a machine with a resolvable hostname and at least one active IPv4 interface, **When** a speed test is run with CSV output, **Then** the CSV output includes `IPAddress` and `Hostname` columns at the end, with the correct values in the data row.

**Scenario: CSV output uses empty values when no IP address or hostname is available**
2. **Given** a machine with no active network interfaces and no resolvable hostname, **When** a speed test is run with CSV output, **Then** the `IPAddress` and `Hostname` columns contain empty values and execution completes normally.

**Scenario: CSV output uses ERROR when hostname retrieval throws**
3. **Given** a condition where fetching the hostname raises an exception, **When** a speed test is run with CSV output, **Then** the `Hostname` column contains `ERROR` and no unhandled exception is surfaced to the user.

---

### User Story 3 - Non-structured output formats are unchanged (Priority: P3)

As a user of NetPace's default rich terminal output or minimal output mode, I want the display to remain unchanged so that adding NMS integration fields does not clutter the human-readable output.

**Why this priority**: Ensures no regression in the existing user experience. The default and minimal output formats are designed for humans, not machines, and device identity fields are irrelevant in that context.

**Independent Test**: Can be fully tested by running `netpace` (default output) and `netpace --output minimal` and confirming neither includes hostname or IP address in the displayed output.

**Acceptance Scenarios**:

**Scenario: Default output does not include hostname or IP address**
1. **Given** any machine, **When** a speed test is run with the default (rich terminal) output, **Then** hostname and IP address are not shown in the output.

**Scenario: Minimal output does not include hostname or IP address**
2. **Given** any machine, **When** a speed test is run with minimal output, **Then** hostname and IP address are not shown in the output.

---

### Edge Cases

- What happens when the machine has multiple network interfaces (e.g., VPN, multiple NICs, loopback)? The first available IPv4 address is selected from all interfaces; loopback addresses may be returned if that is the first IPv4 found.
- What happens when IP address enumeration returns only loopback addresses? The loopback address is returned as the IP (no special filtering).
- What happens when both hostname and IP address retrieval fail? Both fields contain `"ERROR"` independently; the speed test result is still written to output.
- What happens if the machine has both IPv4 and IPv6 on the same interface? IPv4 is always preferred; IPv6 is only selected if no IPv4 address is present on any interface.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: JSON output MUST include an `IPAddress` field (string) positioned after all existing fields.
- **FR-002**: JSON output MUST include a `Hostname` field (string) positioned after the `IPAddress` field.
- **FR-003**: CSV output MUST include an `IPAddress` column positioned after all existing columns.
- **FR-004**: CSV output MUST include a `Hostname` column positioned after the `IPAddress` column.
- **FR-005**: Default rich terminal output and Minimal output MUST NOT include hostname or IP address.
- **FR-006**: The `IPAddress` value MUST be the first available IPv4 address on the device; if no IPv4 address exists, it MUST be the first available IPv6 address.
- **FR-007**: If no IP address (IPv4 or IPv6) is available on any interface, the `IPAddress` field MUST contain an empty string.
- **FR-008**: If retrieving the IP address raises an exception, the `IPAddress` field MUST contain the literal string `ERROR`.
- **FR-009**: The `Hostname` value MUST be the device's resolved hostname as returned by the operating system.
- **FR-010**: If the resolved hostname is an empty string, the `Hostname` field MUST contain an empty string.
- **FR-011**: If retrieving the hostname raises an exception, the `Hostname` field MUST contain the literal string `ERROR`.
- **FR-012**: Failures to retrieve hostname or IP address MUST NOT result in unhandled exceptions propagating to the user.
- **FR-013**: Hostname and IP address retrieval MUST be implemented behind an abstraction (interface) in the Console project; the Core library MUST NOT be modified.
- **FR-014**: The `IPAddress` and `Hostname` fields MUST always be present in JSON and CSV output; they are not conditional on a command-line flag.

### Key Entities

- **ClientInfo**: Represents the identity of the device running NetPace — hostname (string, may be empty or `ERROR`) and IP address (string, may be empty or `ERROR`). This is a presentation-layer concept, not a speed test measurement.
- **IClientInfoProvider**: An abstraction that retrieves hostname and IP address from the local operating system. Enables unit testing without real network infrastructure.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: Every JSON output produced by NetPace includes both `IPAddress` and `Hostname` fields regardless of whether device identity could be determined.
- **SC-002**: Every CSV output produced by NetPace includes both `IPAddress` and `Hostname` columns regardless of whether device identity could be determined.
- **SC-003**: Device identity retrieval failures are handled without crashing — the speed test completes and the affected output fields contain `ERROR` rather than propagating an exception.
- **SC-004**: Engineers using NMS systems such as NetBox or OpenNMS can import NetPace JSON or CSV output and correlate results with existing device records without additional data enrichment steps.
- **SC-005**: The device identity retrieval logic can be fully unit tested in isolation without real network infrastructure.

## Assumptions

- The feature is scoped to the Console project only; no changes are made to the Core (`NetPace.Core`) library or its public API.
- NetPace is pre-version 1.0, so adding new fields to JSON and CSV output is considered acceptable even though it is not strictly backward-compatible with parsers that validate strict schemas.
- No command-line flag is needed to enable or disable these fields — they are always present in structured output formats.
- IP address selection does not filter out loopback addresses (`127.0.0.1`, `::1`); if loopback is the first IPv4 found, it will be used.
- The `IPAddress` field casing follows `IPAddress` (capital I, capital P) to match .NET naming conventions, not `IpAddress`.
- Both fields are appended at the end of existing output: `IPAddress` before `Hostname`.
- The JSON example in the issue showing hostname and IP at the top of the object is illustrative only; the agreed position is at the end of the output.
