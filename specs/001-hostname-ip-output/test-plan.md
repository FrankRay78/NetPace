# Test Plan — Add Hostname and IP Address to Structured Output

## Coverage summary

| Requirement | Primary | Alternate | Error | Boundary | Recovery | Non-functional | Total |
|---|---|---|---|---|---|---|---|
| JSON output includes device identifiers | ✓ | ✓ | ✓ | ✓ | — | — | 9 |
| CSV output includes device identifiers | ✓ | — | ✓ | ✓ | — | — | 5 |
| Default and Minimal output formats are unchanged | ✓ | ✓ | — | — | — | — | 2 |
| IPAddress value reflects device network configuration | ✓ | ✓ | ✓ | ✓ | — | — | 4 |
| Hostname value reflects device identity | ✓ | — | ✓ | ✓ | — | — | 3 |
| Device identity retrieval failures do not interrupt execution | — | — | ✓ | — | — | — | 3 |

**Total scenarios**: 26

**Flags:**
- FR-013 ("Hostname and IP address retrieval MUST be implemented behind an abstraction"): Architecture constraint, not directly testable from outside. Verified structurally via code review. Indirectly confirmed by SC-005 — all scenarios using test doubles assume the abstraction exists and works.
- "Default and Minimal output formats are unchanged": No Error scenarios — appropriate, because these formats do not engage device identity retrieval at all; failures in that layer have no observable effect on these outputs.
- "Device identity retrieval failures do not interrupt execution": No Primary or Alternate scenarios — appropriate, because this requirement exists only to constrain failure behaviour.

---

### Requirement: JSON output includes device identifiers

FR-001, FR-002, FR-006–FR-011, FR-014. JSON output MUST always include `IPAddress` and `Hostname` fields, positioned after all existing fields. Values reflect the device's network identity.

#### Scenario: JSON output contains IPAddress field populated with device IPv4 address
- **WHEN** a speed test completes with `--json` output on a device whose first available IPv4 address is `192.168.1.1`
- **THEN** the JSON output contains the key `"IPAddress"` with value `"192.168.1.1"`

#### Scenario: JSON output contains Hostname field populated with device hostname
- **WHEN** a speed test completes with `--json` output on a device whose hostname resolves to `"test-host"`
- **THEN** the JSON output contains the key `"Hostname"` with value `"test-host"`

#### Scenario: IPAddress field appears after UploadSpeed in JSON output
- **WHEN** a speed test completes with `--json` output
- **THEN** the `"IPAddress"` key appears in the serialised JSON object after the `"UploadSpeed"` key

#### Scenario: Hostname field appears after IPAddress in JSON output
- **WHEN** a speed test completes with `--json` output
- **THEN** the `"Hostname"` key appears in the serialised JSON object immediately after the `"IPAddress"` key

#### Scenario: JSON IPAddress field contains first IPv6 address when no IPv4 is available
- **WHEN** a speed test completes with `--json` output on a device with no IPv4 addresses and whose first available IPv6 address is `"fe80::1"`
- **THEN** the JSON output contains `"IPAddress": "fe80::1"`

#### Scenario: JSON IPAddress field is empty string when no network interfaces are available
- **WHEN** a speed test completes with `--json` output on a device with no active network interfaces
- **THEN** the JSON output contains `"IPAddress": ""`

#### Scenario: JSON Hostname field is empty string when the OS hostname resolves to empty
- **WHEN** a speed test completes with `--json` output on a device whose hostname lookup returns an empty string
- **THEN** the JSON output contains `"Hostname": ""`

#### Scenario: JSON IPAddress field contains ERROR when IP address retrieval raises an exception
- **WHEN** a speed test completes with `--json` output and the device IP address lookup raises an exception
- **THEN** the JSON output contains `"IPAddress": "ERROR"`
- **AND** the `"Hostname"` field is still present in the JSON output

#### Scenario: JSON Hostname field contains ERROR when hostname retrieval raises an exception
- **WHEN** a speed test completes with `--json` output and the device hostname lookup raises an exception
- **THEN** the JSON output contains `"Hostname": "ERROR"`
- **AND** the `"IPAddress"` field is still present in the JSON output

---

### Requirement: CSV output includes device identifiers

FR-003, FR-004, FR-006–FR-011, FR-014. CSV output MUST always include `IPAddress` and `Hostname` columns, positioned after all existing columns. Values reflect the device's network identity.

#### Scenario: CSV header row ends with IPAddress then Hostname columns
- **WHEN** a speed test completes with `--csv` output (first invocation, so the header row is emitted)
- **THEN** the last two column labels in the header row are `IPAddress` then `Hostname`, in that order

#### Scenario: CSV data row contains IPAddress and Hostname values matching device identity
- **WHEN** a speed test completes with `--csv` output on a device with IPv4 address `"10.0.0.1"` and hostname `"router-a"`
- **THEN** the last two values in the data row are `10.0.0.1` then `router-a`, in that order

#### Scenario: CSV IPAddress and Hostname columns contain empty values when device identity is unavailable
- **WHEN** a speed test completes with `--csv` output on a device with no active network interfaces and no resolvable hostname
- **THEN** the IPAddress and Hostname positions in the data row contain empty values (the delimiter appears without content between them)

#### Scenario: CSV IPAddress column contains ERROR when IP address retrieval raises an exception
- **WHEN** a speed test completes with `--csv` output and the device IP address lookup raises an exception
- **THEN** the value in the IPAddress column position of the data row is `ERROR`
- **AND** the Hostname column is still present in the data row

#### Scenario: CSV Hostname column contains ERROR when hostname retrieval raises an exception
- **WHEN** a speed test completes with `--csv` output and the device hostname lookup raises an exception
- **THEN** the value in the Hostname column position of the data row is `ERROR`
- **AND** the IPAddress column is still present in the data row

---

### Requirement: Default and Minimal output formats are unchanged

FR-005. The default rich terminal output and Minimal output MUST NOT include hostname or IP address content.

#### Scenario: Default rich terminal output does not include IPAddress or Hostname
- **WHEN** a speed test completes with the default output format (no output flag specified)
- **THEN** the output does not contain the text `IPAddress`
- **AND** the output does not contain the text `Hostname`

#### Scenario: Minimal output does not include IPAddress or Hostname
- **WHEN** a speed test completes with `--verbosity minimal` output
- **THEN** the output does not contain the text `IPAddress`
- **AND** the output does not contain the text `Hostname`

---

### Requirement: IPAddress value reflects device network configuration

FR-006, FR-007, FR-008. The `IPAddress` value returned by the device identity provider MUST follow a defined selection order: first available IPv4, then first available IPv6, then empty string, then `ERROR` on exception.

#### Scenario: First available IPv4 address is returned when device has IPv4 interfaces
- **WHEN** `GetIPAddress()` is called and the device network layer reports a first unicast IPv4 address of `"10.0.0.5"`
- **THEN** the return value is `"10.0.0.5"`
- **AND** no exception propagates to the caller

#### Scenario: First available IPv6 address is returned when no IPv4 addresses exist
- **WHEN** `GetIPAddress()` is called and the device has no IPv4 unicast addresses but its first IPv6 unicast address is `"fe80::a1b2:c3d4"`
- **THEN** the return value is `"fe80::a1b2:c3d4"`
- **AND** no exception propagates to the caller

#### Scenario: Empty string is returned when no unicast IP addresses are available on any interface
- **WHEN** `GetIPAddress()` is called and the device reports no unicast IPv4 or IPv6 addresses across all interfaces
- **THEN** the return value is `""`
- **AND** no exception propagates to the caller

#### Scenario: ERROR string is returned when network interface enumeration raises an exception
- **WHEN** `GetIPAddress()` is called and the OS network interface API raises an exception
- **THEN** the return value is `"ERROR"`
- **AND** no exception propagates to the caller

---

### Requirement: Hostname value reflects device identity

FR-009, FR-010, FR-011. The `Hostname` value returned by the device identity provider MUST be the OS-resolved device hostname, an empty string if it resolves to empty, or `ERROR` if an exception occurs.

#### Scenario: Device hostname is returned when the OS provides one
- **WHEN** `GetHostname()` is called and the OS reports a hostname of `"gateway-01.example.com"`
- **THEN** the return value is `"gateway-01.example.com"`
- **AND** no exception propagates to the caller

#### Scenario: Empty string is returned when the OS hostname resolves to empty
- **WHEN** `GetHostname()` is called and the OS hostname lookup returns an empty string
- **THEN** the return value is `""`
- **AND** no exception propagates to the caller

#### Scenario: ERROR string is returned when hostname lookup raises an exception
- **WHEN** `GetHostname()` is called and the OS hostname lookup API raises an exception
- **THEN** the return value is `"ERROR"`
- **AND** no exception propagates to the caller

---

### Requirement: Device identity retrieval failures do not interrupt execution

FR-012. Failures to retrieve hostname or IP address MUST NOT result in unhandled exceptions reaching the user. The speed test MUST complete and produce output regardless.

#### Scenario: JSON speed test completes and writes output when IP address retrieval raises an exception
- **WHEN** a speed test is run with `--json` output and the device IP address lookup raises an exception
- **THEN** the process exits with exit code `0`
- **AND** a valid JSON object is written to stdout
- **AND** that JSON object contains the key `"IPAddress"` with value `"ERROR"`

#### Scenario: CSV speed test completes and writes output when hostname retrieval raises an exception
- **WHEN** a speed test is run with `--csv` output and the device hostname lookup raises an exception
- **THEN** the process exits with exit code `0`
- **AND** a CSV header row and data row are written to stdout
- **AND** the data row contains `ERROR` in the Hostname column position

#### Scenario: CSV speed test completes and writes output when both device identity lookups raise exceptions
- **WHEN** a speed test is run with `--csv` output and both the IP address lookup and hostname lookup raise exceptions
- **THEN** the process exits with exit code `0`
- **AND** a CSV header row and data row are written to stdout
- **AND** the data row contains `ERROR` in both the IPAddress and Hostname column positions

#### Scenario: JSON speed test completes and writes output when both device identity lookups raise exceptions
- **WHEN** a speed test is run with `--json` output and both the IP address lookup and hostname lookup raise exceptions
- **THEN** the process exits with exit code `0`
- **AND** a valid JSON object is written to stdout
- **AND** that JSON object contains `"IPAddress": "ERROR"` and `"Hostname": "ERROR"`
