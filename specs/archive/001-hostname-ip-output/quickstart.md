# Quickstart: Add Hostname and IP Address to Structured Output

**Feature**: specs/001-hostname-ip-output  
**Phase**: 1 — Design  
**Date**: 2026-04-14

This guide gives a developer everything they need to begin implementing this feature using TDD.

---

## What We're Building

Adding `IPAddress` and `Hostname` fields to NetPace's JSON and CSV output so that Network Management Systems (NMS) can automatically correlate speed test results with device records.

**Scope**: `NetPace.Console` only. No changes to `NetPace.Core`.

---

## New Interface to Add

Create `src/NetPace.Console/IClientInfoProvider.cs` following the IClock pattern:

```csharp
/// <summary>Interface for retrieving local device identity information.</summary>
public interface IClientInfoProvider
{
    /// <summary>Returns the device's first available IPv4 address; falls back to first IPv6;
    /// returns empty string if none; returns "ERROR" if an exception occurs.</summary>
    string GetIPAddress();

    /// <summary>Returns the device hostname; returns empty string if empty;
    /// returns "ERROR" if an exception occurs.</summary>
    string GetHostname();
}

/// <summary>Production implementation using BCL networking APIs.</summary>
internal sealed class ClientInfoProvider : IClientInfoProvider { ... }

/// <summary>Test stub returning deterministic values.</summary>
internal sealed class ClientInfoProviderStub : IClientInfoProvider
{
    public string IPAddress { get; init; } = "192.168.1.1";
    public string Hostname { get; init; } = "test-host";

    public string GetIPAddress() => IPAddress;
    public string GetHostname() => Hostname;
}
```

---

## TDD Order

Follow RED-GREEN-REFACTOR strictly. Suggested test-writing order:

### Step 1 — IClientInfoProvider unit tests (NetPace.Console.Tests)

Write tests for `ClientInfoProvider` first to drive the production implementation:

1. `GetIPAddress_WhenIPv4Available_ReturnsFirstIPv4Address`
2. `GetIPAddress_WhenNoIPv4ButIPv6Available_ReturnsFirstIPv6Address`
3. `GetIPAddress_WhenNoInterfaces_ReturnsEmptyString`
4. `GetHostname_WhenHostnameAvailable_ReturnsHostname`
5. `GetHostname_WhenHostnameIsEmpty_ReturnsEmptyString`

### Step 2 — JSON integration tests (NetPace.Console.Tests)

Use `CommandAppTester` + `ClientInfoProviderStub` + snapshot assertions (Verify):

1. JSON output includes `IPAddress` field with stub value
2. JSON output includes `Hostname` field with stub value  
3. Both fields appear after `UploadSpeed` in the output
4. `IPAddress` appears before `Hostname`
5. JSON output with empty IPAddress (`ClientInfoProviderStub { IPAddress = "" }`)
6. JSON output with ERROR hostname (`ClientInfoProviderStub { Hostname = "ERROR" }`)

### Step 3 — CSV integration tests (NetPace.Console.Tests)

1. CSV header includes `IPAddress` and `Hostname` columns at end
2. CSV data row includes stub values
3. CSV with empty IPAddress
4. CSV with ERROR hostname

### Step 4 — Default/Minimal output regression tests

1. Default output does not contain "IPAddress" or "Hostname" text
2. Minimal output does not contain "IPAddress" or "Hostname" text

---

## Files to Create or Modify

| File | Change |
|------|--------|
| `src/NetPace.Console/IClientInfoProvider.cs` | **Create** — interface + implementations + stubs |
| `src/NetPace.Console/JsonResult.cs` | **Modify** — add `IPAddress` and `Hostname` required properties |
| `src/NetPace.Console/IConsoleWriter.cs` | **Modify** — add `IClientInfoProvider` parameter to `PerformSpeedTestAsync` |
| `src/NetPace.Console/Commands/SpeedTestCommand.cs` | **Modify** — inject `IClientInfoProvider`, pass to writers |
| `src/NetPace.Console/ConsoleWriters/JsonConsoleWriter.cs` | **Modify** — populate `IPAddress` and `Hostname` in `JsonResult` |
| `src/NetPace.Console/ConsoleWriters/CSVConsoleWriter.cs` | **Modify** — add columns to header and data rows |
| `src/NetPace.Console/ConsoleWriters/DefaultConsoleWriter.cs` | **Modify** — accept new parameter (unused) |
| `src/NetPace.Console/ConsoleWriters/MinimalConsoleWriter.cs` | **Modify** — accept new parameter (unused) |
| `src/NetPace.Console/Program.cs` | **Modify** — register `IClientInfoProvider` in DI |
| `src/NetPace.Console.Tests/NetPaceConsoleTests.Json.cs` | **Modify** — new test cases + update existing snapshots |
| `src/NetPace.Console.Tests/NetPaceConsoleTests.CSV.cs` | **Modify** — new test cases + update existing snapshots |
| `src/NetPace.Console.Tests/Expectations/*.verified.txt` | **Update** — existing snapshots need refreshing (new fields added) |

---

## Registration Pattern (Program.cs)

```csharp
// Production path
registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProvider));

// Test path (--test flag)
registrar.Register(typeof(IClientInfoProvider), typeof(ClientInfoProviderStub));
```

---

## Snapshot Testing Notes

Existing JSON and CSV `.verified.txt` snapshots will fail once `JsonResult` and `CSVConsoleWriter` are updated because the output shape changes. This is expected — run tests, review the diff, accept the new snapshots with the Verify tooling if they are correct.

New test methods need new `.verified.txt` files. Follow the existing naming convention:
- `NetPaceConsoleTests.Json.Should_Include_IPAddress_And_Hostname_In_Json_Output.verified.txt`

---

## Running Tests

```bash
dotnet test src/NetPace.Console.Tests
```

To accept new Verify snapshots (after confirming they are correct):

```bash
dotnet test src/NetPace.Console.Tests -- --verify-accept-snapshots
```
