# Data Model: Add Hostname and IP Address to Structured Output

**Feature**: specs/001-hostname-ip-output  
**Phase**: 1 — Design  
**Date**: 2026-04-14

## New Types

### IClientInfoProvider (interface)

**Location**: `src/NetPace.Console/IClientInfoProvider.cs`  
**Purpose**: Abstraction for retrieving the local device's hostname and IP address. Enables unit testing without real OS calls.

```
IClientInfoProvider
├── GetIPAddress() : string
│   Returns the first available IPv4 address, falling back to first IPv6,
│   then empty string if none. Returns "ERROR" if an exception is thrown.
│
└── GetHostname() : string
    Returns the device hostname. Returns empty string if hostname is empty.
    Returns "ERROR" if an exception is thrown.
```

**Invariants**:
- Neither method ever throws — all exceptions are caught internally.
- Return value is always a non-null string.

### ClientInfoProvider (production implementation)

**Location**: `src/NetPace.Console/IClientInfoProvider.cs` (same file as interface, following IClock pattern)  
**Implements**: `IClientInfoProvider`

**GetIPAddress() logic**:
1. Call `NetworkInterface.GetAllNetworkInterfaces()`
2. For each interface, enumerate `UnicastAddresses`
3. Return the first address where `AddressFamily == InterNetwork` (IPv4)
4. If none found, return first where `AddressFamily == InterNetworkV6` (IPv6)
5. If none found, return `string.Empty`
6. On any exception: return `"ERROR"`

**GetHostname() logic**:
1. Call `Dns.GetHostName()`
2. If result is null or empty: return `string.Empty`
3. On any exception: return `"ERROR"`

### ClientInfoProviderStub (test stub)

**Location**: `src/NetPace.Console/IClientInfoProvider.cs` (same file, following ClockStub pattern)  
**Implements**: `IClientInfoProvider`  
**Purpose**: Returns deterministic values in tests.

```
ClientInfoProviderStub
├── IPAddress : string = "192.168.1.1"  (configurable)
└── Hostname  : string = "test-host"   (configurable)
```

### ExceptionThrowingClientInfoProviderStub (test stub)

**Location**: `src/NetPace.Console/IClientInfoProvider.cs` (same file)  
**Implements**: `IClientInfoProvider`  
**Purpose**: Simulates exception conditions — returns `"ERROR"` for both fields, as the production implementation would when exceptions are thrown internally.

---

## Updated Types

### JsonResult (record)

**Location**: `src/NetPace.Console/JsonResult.cs`  
**Change**: Add two new required properties at the end.

Current fields (unchanged):
- `ServerLocation` (string)
- `ServerSponsor` (string)
- `ServerUrl` (string)
- `Timestamp` (string)
- `Latency` (string)
- `DownloadSpeed` (string)
- `UploadSpeed` (string)

**New fields** (appended in this order):
- `IPAddress` (string) — populated from `IClientInfoProvider.GetIPAddress()`
- `Hostname` (string) — populated from `IClientInfoProvider.GetHostname()`

**Note**: Property declaration order controls JSON serialisation order with `System.Text.Json` default settings. `IPAddress` and `Hostname` must be declared after the existing properties.

### IConsoleWriter (interface)

**Location**: `src/NetPace.Console/IConsoleWriter.cs`  
**Change**: Add `IClientInfoProvider clientInfoProvider` parameter to `PerformSpeedTestAsync`.

Updated signature:
```
PerformSpeedTestAsync(
    bool initialSpeedTest,
    IAnsiConsole console,
    IClock clock,
    IClientInfoProvider clientInfoProvider,
    ISpeedTestService speedTestClient,
    SpeedTestCommandSettings settings,
    CancellationToken cancellationToken
) : Task
```

**Parameter position**: After `clock`, before `speedTestClient` — keeping device-identity dependencies grouped with other environmental services.

### SpeedTestCommand

**Location**: `src/NetPace.Console/Commands/SpeedTestCommand.cs`  
**Change**: Add `IClientInfoProvider` to constructor injection, pass to `PerformSpeedTestAsync`.

### DefaultConsoleWriter, MinimalConsoleWriter

**Location**: `src/NetPace.Console/ConsoleWriters/`  
**Change**: Accept new parameter in `PerformSpeedTestAsync` — no further changes. The parameter is received but not used.

### JsonConsoleWriter

**Location**: `src/NetPace.Console/ConsoleWriters/JsonConsoleWriter.cs`  
**Change**: Call `clientInfoProvider.GetIPAddress()` and `clientInfoProvider.GetHostname()` when populating `JsonResult`.

### CSVConsoleWriter

**Location**: `src/NetPace.Console/ConsoleWriters/CSVConsoleWriter.cs`  
**Change**:
- Add `IPAddress` and `Hostname` to the header row (after existing columns, in both with-units and without-units modes).
- Add corresponding data values to each data row (never filtered by null — always present).

---

## DI Registration

**Location**: `src/NetPace.Console/Program.cs`  
**Change**: Register `IClientInfoProvider` alongside `IClock`.

```
Production:  Register(IClientInfoProvider, ClientInfoProvider)
Test (--test flag): Register(IClientInfoProvider, ClientInfoProviderStub)
```

---

## Dependency Map

```
SpeedTestCommand
  └── IClientInfoProvider (injected via constructor)
       ├── Passed to JsonConsoleWriter.PerformSpeedTestAsync
       │    └── clientInfoProvider.GetIPAddress()  → JsonResult.IPAddress
       │    └── clientInfoProvider.GetHostname()   → JsonResult.Hostname
       └── Passed to CSVConsoleWriter.PerformSpeedTestAsync
            └── clientInfoProvider.GetIPAddress()  → CSV IPAddress column
            └── clientInfoProvider.GetHostname()   → CSV Hostname column
```

`DefaultConsoleWriter` and `MinimalConsoleWriter` receive `IClientInfoProvider` via the interface but do not use it.
