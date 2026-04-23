# Research: Add Hostname and IP Address to Structured Output

**Feature**: specs/001-hostname-ip-output  
**Phase**: 0 — Research  
**Date**: 2026-04-14

## Decisions

### 1. Layer Ownership

**Decision**: Console project only — no changes to `NetPace.Core`.

**Rationale**: Hostname and IP address are device identity facts about _where_ the test ran, not measurements produced _by_ the speed test. The existing precedent in the codebase is `Timestamp`, which is populated by `IClock` in the Console layer. Hostname/IP belong in the same category. Adding them to Core result types would expand the NuGet API surface unnecessarily and introduce an OS-interrogation concern into a network measurement library.

**Alternatives considered**:
- Adding to `NetPace.Core` result DTOs — rejected because Core is a published NuGet package; adding machine identity fields to speed test result types would be semantically incorrect and surprising to library consumers.
- Surfacing public IP via Ookla API — rejected because the Ookla client does not currently surface this; it adds a dependency on undocumented API internals. Local IP is sufficient for NMS correlation.

---

### 2. Abstraction Design — IClientInfoProvider

**Decision**: Introduce `IClientInfoProvider` interface in the Console project (root level, alongside `IClock`), following the exact same DI and injection pattern.

**Rationale**:
- `IClock` already establishes the pattern: interface at root of `NetPace.Console`, production implementation + test stubs in same file, injected into `SpeedTestCommand` via constructor, passed to `PerformSpeedTestAsync` as a method parameter.
- This pattern makes the new dependency fully testable in unit tests without real OS calls.
- Consistent with the existing architecture — no new patterns introduced.

**Interface design**:
```
IClientInfoProvider
  GetIPAddress() → string   (empty or "ERROR" — never throws)
  GetHostname()  → string   (empty or "ERROR" — never throws)
```

The production implementation wraps `System.Net.NetworkInformation.NetworkInterface` and `System.Net.Dns.GetHostName()`. All exceptions are caught internally; the caller always receives a safe string value.

**Alternatives considered**:
- Single `GetClientInfo()` method returning a `ClientInfo` record — considered, but splitting into two methods allows finer-grained stub control in tests. A single method can still be used if a `ClientInfo` record is preferred; this is a minor design choice.
- Passing `ClientInfo` as a resolved value (not interface) to writers — rejected because it prevents the writers from being tested independently.

---

### 3. IConsoleWriter Interface Change

**Decision**: Add `IClientInfoProvider clientInfoProvider` as a new parameter to `IConsoleWriter.PerformSpeedTestAsync`.

**Rationale**: IClock is already passed this way. The method signature already has: `bool initialSpeedTest, IAnsiConsole console, IClock clock, ISpeedTestService speedTestClient, SpeedTestCommandSettings settings, CancellationToken cancellationToken`. Adding `IClientInfoProvider` follows the established pattern. All 4 writers (Default, Minimal, JSON, CSV) get the parameter; only JSON and CSV use it. Default and Minimal ignore it, which is correct behavior.

**Alternatives considered**:
- Injecting into writer constructors via DI — writers are instantiated with `new` in a switch expression in `SpeedTestCommand`, not resolved from DI. Switching to DI-based writer resolution would be a larger refactor not justified by this feature.
- Calling `IClientInfoProvider` once in `SpeedTestCommand` before dispatching to the writer, passing `ClientInfo` as a method parameter — feasible, but adds a new type to the method signature whereas passing the interface is consistent with the IClock precedent.

---

### 4. IP Address Selection Logic

**Decision**: Use `System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()` to enumerate all interfaces, then select the first `UnicastIPAddressInformation` with `AddressFamily.InterNetwork` (IPv4); if none found, select the first with `AddressFamily.InterNetworkV6` (IPv6); if neither found, return empty string. Loopback addresses are not explicitly excluded.

**Rationale**: The project owner specified this exact logic in the issue thread. The BCL API is cross-platform (Windows, Linux, macOS) and requires no new dependencies. Loopback is not filtered per owner decision — if loopback is the only interface, it will be returned.

**Exception handling**: Any `System.Exception` (including `NetworkInformationException`, `PlatformNotSupportedException`) is caught and returns the literal string `"ERROR"`.

---

### 5. Hostname Resolution

**Decision**: Use `System.Net.Dns.GetHostName()`. If the result is an empty string, return empty string. If an exception is thrown (including `SocketException`), return the literal string `"ERROR"`.

**Rationale**: `Dns.GetHostName()` is the standard, cross-platform BCL method. It's synchronous, low-overhead, and works on all .NET 8 platforms.

---

### 6. Field Placement and Casing

**Decision**: `IPAddress` (capital I, capital P) field first, then `Hostname`, both appended after all existing fields in both JSON and CSV output.

**Rationale**: Specified explicitly by the project owner in the issue thread. `IPAddress` casing matches the .NET BCL type name convention (`System.Net.IPAddress`). Field order at end preserves backward compatibility for positional CSV consumers that ignore trailing columns.

---

### 7. Always-On vs Opt-In

**Decision**: Always emit both fields in JSON and CSV — no flag required.

**Rationale**: NetPace is pre-v1.0. Breaking changes are acceptable. Making these fields always-present is simpler and ensures NMS parsers always get a consistent schema.

---

### 8. Test Approach

**Decision**: Follow the existing snapshot testing pattern in `NetPace.Console.Tests` — use `CommandAppTester` with a `ClientInfoProviderStub` that returns deterministic values, then compare output against `.verified.txt` snapshots using the Verify library.

**Rationale**: The existing JSON and CSV tests already use `ClockStub` for deterministic timestamps. A `ClientInfoProviderStub` returning fixed hostname/IP values (e.g., `"test-host"`, `"192.168.1.1"`) follows the same pattern and produces deterministic, verifiable snapshots.

**New stub needed**: `ClientInfoProviderStub` (fixed values) and potentially `ExceptionThrowingClientInfoProviderStub` (for error handling tests). Both live in the same file as `IClientInfoProvider`, consistent with how `ClockStub` lives in `IClock.cs`.

---

## Cross-Platform API Verification

| API | Windows | Linux | macOS | Notes |
|-----|---------|-------|-------|-------|
| `Dns.GetHostName()` | ✅ | ✅ | ✅ | May return short name or FQDN depending on OS config |
| `NetworkInterface.GetAllNetworkInterfaces()` | ✅ | ✅ | ✅ | May require elevated permissions in some Linux environments; exceptions caught |
| `UnicastIPAddressInformation.Address` | ✅ | ✅ | ✅ | `IPAddress.AddressFamily` available on all platforms |

All APIs are .NET BCL. No new package dependencies required.
