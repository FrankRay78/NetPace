# NetPace for IoT: MVP Implementation Plan

**Target Market**: Cellular IoT (LTE-M/NB-IoT), embedded devices, fleet management
**Timeline**: 6-8 weeks for MVP
**Market Size**: $11.2B (2025) → $36.3B (2034), 14% CAGR
**Critical Pain Point**: 74% of IoT pilots fail due to connectivity issues

---

## Executive Summary

NetPace's existing architecture is **exceptionally well-suited for IoT** with zero external dependencies, interface-driven design, and already supports ARM64. This is a **feature expansion**, not an architectural rewrite. The current 148KB binary + 3MB dependencies already meets IoT size requirements.

**MVP delivers 4 critical features**:
1. **Micro test profile** - < 1MB data usage (vs current 100MB+)
2. **Scheduled testing** - Automated monitoring via cron/systemd
3. **GPS location support** - Coverage mapping for fleet deployment
4. **Cellular signal metrics** - RSSI, RSRQ, RSRP, SINR for connectivity quality

**Success Criteria**: 5-10 pilot customers (fleet managers, device manufacturers) running automated tests in production within 12 weeks.

---

## 1. MVP Feature Scope (User-Confirmed)

### P0: Minimal Data Usage Mode (Week 1-2)
**Business Value**: #1 blocker for IoT - current tests use 100MB+, IoT plans are 10-100MB/month
**Technical Approach**: Configuration preset for `OoklaSpeedtestSettings`
- Latency: 3 pings (vs 20 default)
- Download: 1× 350KB file (vs 5 files × 4 iterations)
- Upload: 1× 100KB payload (vs 25MB+ incremental)
- **Total data usage: ~450KB** ✓

**Implementation**:
```csharp
// New file: src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.Presets.cs
public static class OoklaSpeedtestSettingsPresets
{
    public static OoklaSpeedtestSettings Micro => new()
    {
        LatencyTest = new() { LatencyTestIterations = 3 },
        DownloadTest = new()
        {
            DownloadSizes = new[] { 350 },
            DownloadSizeIterations = 1,
            DownloadParallelTasks = 1
        },
        UploadTest = new()
        {
            UploadIncrements = 1,
            UploadSizeIncrementKb = 100,
            UploadSizeIterations = 1,
            UploadParallelTasks = 1
        }
    };
}
```

**CLI**: `NetPace --profile micro`

---

### P0: Scheduled Testing Infrastructure (Week 2-3)
**Business Value**: Core IoT use case - devices run tests automatically, alert on failures
**Technical Approach**: Exit codes + threshold validation + append mode

**Features**:
- Exit codes: 0=pass, 1=fail, 2=degraded
- Threshold flags: `--min-download 1.0`, `--max-latency 500`
- Append mode: `--file results.csv --file-mode Append` (already exists!)
- Examples: systemd service, cron jobs

**Implementation**:
- Modify `SpeedTestCommand` to return exit code based on thresholds
- Add validation for threshold options in `SpeedTestCommandSettings`
- Create deployment examples

**Usage**:
```bash
# Systemd: Run every 15 minutes, alert if < 1 Mbps
NetPace --profile micro --min-download 1.0 --file /var/log/netpace.csv

# Cron: Daily site survey
0 2 * * * /usr/local/bin/NetPace --profile micro --location auto >> /var/log/connectivity.log
```

---

### P0: Location-Based Testing (Week 3-4)
**Business Value**: Fleet management needs GPS coordinates for coverage mapping
**Technical Approach**: Optional `Location` property on `SpeedTestResult`

**Features**:
- Manual input: `--location "37.7749,-122.4194"`
- Auto-detect (Linux only): `--location auto` (reads from gpsd)
- CSV/JSON output includes latitude, longitude columns

**Implementation**:
```csharp
// New: src/NetPace.Core/Location.cs
public sealed record Location(double Latitude, double Longitude);

// Modified: src/NetPace.Core/SpeedTestResult.cs
public sealed record SpeedTestResult
{
    public long BytesProcessed { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public Location? Location { get; init; } // NEW - optional
}

// New: src/NetPace.Console/LocationProviders/ILocationProvider.cs
public interface ILocationProvider
{
    Task<Location?> GetLocationAsync(CancellationToken ct);
}
```

**CSV Output**:
```
Timestamp,Latitude,Longitude,Latency,Download,Upload
2025-12-10 14:35:00,37.7749,-122.4194,45,15.2,3.4
```

---

### P0: Cellular Signal Metrics (Week 4-6)
**Business Value**: Differentiates from desktop tool, enables connectivity diagnostics
**Technical Approach**: New interface + optional provider implementation

**Features**:
- Signal metrics: RSSI, RSRQ, RSRP, SINR (cellular quality indicators)
- AT command support for Linux modems (Quectel, Telit, Sierra Wireless)
- Optional feature - works with stub for non-cellular devices

**Implementation**:
```csharp
// New: src/NetPace.Core/ICellularModemProvider.cs
public interface ICellularModemProvider
{
    Task<CellularMetrics?> GetMetricsAsync(CancellationToken ct = default);
}

// New: src/NetPace.Core/CellularMetrics.cs
public sealed record CellularMetrics
{
    public int RSSI { get; init; }       // Signal strength (dBm)
    public double RSRQ { get; init; }    // Signal quality (dB)
    public int RSRP { get; init; }       // Reference signal power (dBm)
    public double SINR { get; init; }    // Signal-to-noise ratio (dB)
    public string? Carrier { get; init; }
    public string? NetworkType { get; init; } // LTE, LTE-M, NB-IoT, 5G
}

// Modified: src/NetPace.Core/SpeedTestResult.cs
public sealed record SpeedTestResult
{
    public long BytesProcessed { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public Location? Location { get; init; }
    public CellularMetrics? CellularMetrics { get; init; } // NEW - optional
}

// New PROJECT: src/NetPace.Core.CellularProviders/
// - Linux/LinuxAtCommandModemProvider.cs (AT+CSQ, AT+CESQ via /dev/ttyUSB0)
// - Stubs/NullModemProvider.cs (returns null for non-cellular)
```

**CLI**: `NetPace --cellular --profile micro`

**AT Commands Used**:
- `AT+CSQ` → RSSI (0-31 scale converts to dBm)
- `AT+CESQ` → RSRQ, RSRP, SINR
- `AT+COPS?` → Carrier name
- References: [M2MSupport AT Commands](https://m2msupport.net/m2msupport/atcsq-signal-quality/), [POYNTING Signal Strength Cheat Sheet](https://poynting.tech/articles/antenna-faq/signal-strength-measure-rsrp-rsrq-and-sinr-reference-for-lte-cheat-sheet/)

---

## 2. Architecture Decisions

### Decision 1: Stay with .NET 8 (NOT Go, NOT Rust)
**Rationale**:
- Current 3.1MB total already meets < 5MB requirement
- .NET 8 AOT can reduce to ~1.5MB if needed ([Native AOT docs](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/))
- Memory: ~35MB runtime, well under 50MB target
- Rewriting 2,076 lines + tests would delay 6+ months
- Maintains NetPace.Core library compatibility

**Action**: Investigate AOT as optimization (Week 6), but NOT blocking for MVP

### Decision 2: Micro Test = Configuration Preset
**Rationale**:
- `OoklaSpeedtestSettings` already supports full customization
- No protocol changes needed, just preset configurations
- Maintains backward compatibility
- TDD-friendly: test data usage calculations

### Decision 3: Progressive Enhancement Pattern
**Rationale**:
- Add optional properties to `SpeedTestResult` (Location?, CellularMetrics?)
- Non-breaking changes for existing NuGet consumers
- Features work independently (can use location without cellular, etc.)
- Follows existing pattern: `IProgress<T>` is already optional

### Decision 4: Separate Cellular Provider Project
**Rationale**:
- Keeps NetPace.Core zero-dependency
- Platform-specific code isolated in `NetPace.Core.CellularProviders`
- Optional NuGet package for IoT users
- Enables manufacturer-specific providers (Quectel, Telit, SIMCom)

---

## 3. Work Packages (Parallelizable via Git Worktrees)

### Package A: Micro Test Profile
**Owner**: Developer 1
**Duration**: Week 1-2 (5 days)
**Dependencies**: None
**Size**: Small

**Tasks**:
1. Create `OoklaSpeedtestSettingsPresets.cs` with TDD
2. Add `--profile [micro|standard]` CLI option
3. Wire up profile selection in `SpeedTestCommand`
4. Write unit tests for data usage validation
5. Document in USER_GUIDE.md

**Files Modified**:
- `src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.Presets.cs` (NEW)
- `src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`
- `src/NetPace.Console/Commands/SpeedTestCommand.cs`
- `src/NetPace.Core.Tests/OoklaSpeedtestSettingsPresetsTests.cs` (NEW)
- `USER_GUIDE.md`

**Git Worktree**:
```bash
git worktree add ../netpace-micro feature/micro-test-profile
```

---

### Package B: Scheduled Testing
**Owner**: Developer 2
**Duration**: Week 2-3 (5 days)
**Dependencies**: None (can run parallel with Package A)
**Size**: Small

**Tasks**:
1. Add threshold CLI options (`--min-download`, `--max-latency`)
2. Implement exit code logic (0/1/2)
3. Validate append mode works correctly
4. Create systemd service example
5. Create cron examples
6. Write integration tests for threshold checking

**Files Modified**:
- `src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`
- `src/NetPace.Console/Commands/SpeedTestCommand.cs`
- `examples/systemd/netpace.service` (NEW)
- `examples/cron/README.md` (NEW)
- `src/NetPace.Console.Tests/ThresholdTests.cs` (NEW)

**Git Worktree**:
```bash
git worktree add ../netpace-scheduled feature/scheduled-testing
```

**Merge Conflict**: Minor conflict with Package A in `SpeedTestCommandSettings.cs` - easy to resolve

---

### Package C: Location Support
**Owner**: Developer 1 (after Package A merges)
**Duration**: Week 3-4 (7 days)
**Dependencies**: Package A should merge first
**Size**: Medium

**Tasks**:
1. Create `Location` record in NetPace.Core
2. Add optional `Location?` to `SpeedTestResult`
3. Create `ILocationProvider` interface
4. Implement `ManualLocationProvider` (parses "lat,lon")
5. Implement `GpsdLocationProvider` (Linux only, optional)
6. Add `--location` CLI option
7. Update all console writers (CSV, JSON, Minimal)
8. Write unit tests for location parsing
9. Write integration test with gpsd stub

**Files Modified**:
- `src/NetPace.Core/Location.cs` (NEW)
- `src/NetPace.Core/SpeedTestResult.cs`
- `src/NetPace.Console/LocationProviders/ILocationProvider.cs` (NEW)
- `src/NetPace.Console/LocationProviders/ManualLocationProvider.cs` (NEW)
- `src/NetPace.Console/LocationProviders/GpsdLocationProvider.cs` (NEW)
- `src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`
- `src/NetPace.Console/ConsoleWriters/CSVConsoleWriter.cs`
- `src/NetPace.Console/ConsoleWriters/JsonConsoleWriter.cs`
- `src/NetPace.Console/ConsoleWriters/MinimalConsoleWriter.cs`
- `src/NetPace.Core.Tests/LocationTests.cs` (NEW)

**Git Worktree**:
```bash
git worktree add ../netpace-location feature/location-support
```

**Merge Conflicts**: Moderate - conflicts with A & B in settings files, multiple writer files

---

### Package D: Cellular Signal Metrics
**Owner**: Developer 1 (after Package C merges)
**Duration**: Week 4-6 (10 days)
**Dependencies**: Package C should merge first
**Size**: Large

**Tasks**:
1. Create `ICellularModemProvider` interface (TDD)
2. Create `CellularMetrics` record
3. Add optional `CellularMetrics?` to `SpeedTestResult`
4. NEW PROJECT: `NetPace.Core.CellularProviders`
5. Implement `LinuxAtCommandModemProvider` (serial port + AT commands)
6. Implement `NullModemProvider` (stub)
7. Add `--cellular` CLI flag
8. Update console writers for signal columns
9. Write unit tests with mocked serial port
10. Hardware validation on real modem (Week 7-8)

**Files Modified**:
- `src/NetPace.Core/ICellularModemProvider.cs` (NEW)
- `src/NetPace.Core/CellularMetrics.cs` (NEW)
- `src/NetPace.Core/SpeedTestResult.cs`
- `src/NetPace.Core.CellularProviders/NetPace.Core.CellularProviders.csproj` (NEW PROJECT)
- `src/NetPace.Core.CellularProviders/Linux/LinuxAtCommandModemProvider.cs` (NEW)
- `src/NetPace.Core.CellularProviders/Stubs/NullModemProvider.cs` (NEW)
- `src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`
- `src/NetPace.Console/Commands/SpeedTestCommand.cs`
- `src/NetPace.Console/ConsoleWriters/*.cs` (all writers)
- `src/NetPace.Core.Tests/CellularMetricsTests.cs` (NEW)

**Git Worktree**:
```bash
git worktree add ../netpace-cellular feature/cellular-metrics
```

**Merge Conflicts**: Major - conflicts with Package C in `SpeedTestResult.cs` and all writers. Requires careful merge.

---

### Package E: Documentation & Examples
**Owner**: Developer 4 OR Technical Writer
**Duration**: Week 5-6 (concurrent with integration testing)
**Dependencies**: All packages A-D merged
**Size**: Small

**Tasks**:
1. Create `IOT_GUIDE.md` with deployment patterns
2. Update README.md with IoT use cases
3. Create Raspberry Pi installation guide
4. Create Docker Compose example for edge gateways
5. Create fleet management setup guide
6. Document cellular modem requirements
7. Create troubleshooting guide

**Files Created**:
- `IOT_GUIDE.md` (NEW)
- `examples/raspberry-pi/INSTALL.md` (NEW)
- `examples/docker/docker-compose.yml` (NEW)
- `examples/fleet-management/SETUP.md` (NEW)
- `docs/TROUBLESHOOTING_CELLULAR.md` (NEW)
- `README.md` (update IoT section)

**Git Worktree**:
```bash
git worktree add ../netpace-docs docs/iot-guide
```

**Merge Conflicts**: None - documentation only

---

## 4. Implementation Sequence & Timeline

### Week 1-2: Foundation Phase
- **Day 1-2**: Setup project board, create issues, hardware procurement
- **Day 3-7**: Package A (Micro Profile) → Developer 1
- **Day 3-9**: Package B (Scheduled Testing) → Developer 2
- **Day 10**: Merge Package A to main
- **Day 12**: Merge Package B to main (resolve settings conflicts)

### Week 3-4: Core IoT Features
- **Day 15-21**: Package C (Location) → Developer 1
- **Day 22**: Merge Package C to main
- **Day 23-28**: Package D (Cellular) → Developer 1
- **Day 28**: Merge Package D to main

### Week 5-6: Polish & Integration
- **Day 29-35**: Package E (Documentation) → Developer 4
- **Day 29-38**: Integration testing, bug fixes
- **Day 36**: Merge Package E to main
- **Day 38-42**: Hardware validation preparation

### Week 7-8: Hardware Validation
- **Day 43-49**: Test on real hardware (Raspberry Pi + Quectel modem)
- **Day 50-56**: Bug fixes, refinements based on hardware tests
- **Day 56**: MVP Release 🚀

**Critical Path**: A → B → C → D → E → Hardware Validation

---

## 5. Testing Strategy (TDD Throughout)

### Unit Testing (TDD - No Hardware)
**Target**: Maintain > 80% code coverage

**Example Test (Package A)**:
```csharp
[Fact]
public void Micro_Preset_Should_Use_Less_Than_1MB_Data()
{
    var preset = OoklaSpeedtestSettingsPresets.Micro;

    preset.LatencyTest.LatencyTestIterations.ShouldBe(3);
    preset.DownloadTest.DownloadSizes.ShouldBe(new[] { 350 });

    var estimatedUsage = CalculateEstimatedDataUsage(preset);
    estimatedUsage.ShouldBeLessThan(1_000_000); // < 1MB
}
```

**Example Test (Package D)**:
```csharp
[Fact]
public async Task LinuxAtCommandModemProvider_Should_Parse_RSSI_From_CSQ()
{
    var mockSerial = new MockSerialPort();
    mockSerial.SetResponse("AT+CSQ", "+CSQ: 22,99\r\nOK");

    var provider = new LinuxAtCommandModemProvider(mockSerial);
    var metrics = await provider.GetMetricsAsync();

    metrics.RSSI.ShouldBe(-69); // 22 on 0-31 scale = -69 dBm
    metrics.Carrier.ShouldNotBeNull();
}
```

### Integration Testing (Real Network)
**Existing Pattern**: `NetPace.Core.Tests` already has integration tests

```csharp
[Fact]
[Trait("Category", "Integration")]
public async Task Micro_Profile_Should_Complete_Under_1MB()
{
    var settings = OoklaSpeedtestSettingsPresets.Micro;
    var speedtest = new OoklaSpeedtest(settings);

    var servers = await speedtest.GetServersAsync();
    var result = await speedtest.GetDownloadSpeedAsync(servers.First());

    result.BytesProcessed.ShouldBeLessThan(500_000); // < 500KB
}
```

### Hardware Validation (Week 7-8)

**Test Bench Setup**:
- Raspberry Pi 4 (ARM64) - already owned
- Raspberry Pi Zero W (ARM32) - **purchase recommended**
- Quectel EC25 LTE modem - **purchase: ~$30**
- Active LTE-M/NB-IoT SIM card - **purchase: ~$10/month**
- USB-to-serial adapter - **purchase: ~$15**

**Total Hardware Investment: ~$300** (user-approved)

**ARM32 Testing Strategy**:
1. **QEMU ARM Emulation** - For CI/CD automated testing ([QEMU ARM](https://www.qemu.org/docs/master/system/target-arm.html))
2. **Real Raspberry Pi Zero** - For final validation before release

**QEMU Setup**:
```bash
# Install QEMU ARM emulator
sudo apt-get install qemu-user-static

# Build ARM32 binary
dotnet publish -r linux-arm -c Release

# Test with QEMU
qemu-arm-static ./NetPace --profile micro
```

**Manual Test Checklist**:
- [ ] Micro test completes in < 10 seconds
- [ ] Data usage < 1MB verified via carrier portal
- [ ] Signal metrics match modem display values
- [ ] GPS coordinates from gpsd match Google Maps
- [ ] CSV imports correctly into Excel/InfluxDB
- [ ] Systemd service restarts on failure
- [ ] Cron job runs every 15 minutes successfully
- [ ] ARM32 build works on Pi Zero
- [ ] ARM64 build works on Pi 4

---

## 6. GitHub Project Board Structure

**Create during implementation kickoff** (user-approved)

### Columns
1. **Backlog** - All issues not yet prioritized
2. **Ready** - Issues ready to start (acceptance criteria defined)
3. **In Progress** - Currently being worked on
4. **In Review** - PR open, awaiting review
5. **Testing** - Merged to main, needs hardware validation
6. **Done** - Validated and documented

### Milestones
- **M1: Data Reduction** (Week 2) - Micro test profile working
- **M2: Automation Ready** (Week 3) - Scheduled testing + exit codes
- **M3: Location Aware** (Week 4) - GPS coordinate support
- **M4: IoT Signals** (Week 6) - Cellular metrics integration
- **M5: MVP Release** (Week 8) - All features integrated, validated

### Labels
- `priority: p0` - MVP blocker
- `priority: p1` - MVP nice-to-have
- `priority: p2` - Post-MVP
- `component: core` - NetPace.Core library
- `component: console` - CLI application
- `component: cellular` - Cellular provider code
- `component: docs` - Documentation
- `size: small` - < 1 day
- `size: medium` - 1-3 days
- `size: large` - 3-5 days
- `platform: linux` - Linux-specific
- `platform: arm64` - ARM64 architecture
- `platform: arm32` - ARM32 architecture
- `hardware-required` - Needs real device testing

---

## 7. Risk Assessment & Mitigation

### Risk 1: Hardware Dependency - Cellular Modem Testing
**Probability**: High
**Impact**: High - Can't validate cellular metrics without hardware

**Mitigation**:
- ✅ **Immediate**: Purchase Quectel EC25 modem (~$30) + Pi Zero (~$35)
- ✅ **Week 1**: Set up hardware test bench
- ✅ **Testing Strategy**:
  - Unit tests with mocked serial port (no hardware required)
  - Stub provider for CI/CD
  - Real hardware validation in Week 7-8 only
- ✅ **QEMU ARM32 emulation** for CI/CD testing (user suggestion)

**Cost**: ~$300 total (user-approved)

### Risk 2: AT Command Compatibility Across Modems
**Probability**: Medium
**Impact**: Medium - Different manufacturers use different AT commands

**Mitigation**:
- Focus MVP on Quectel modems (most common in IoT)
- Document supported modem models clearly
- `ICellularModemProvider` allows manufacturer-specific implementations
- Post-MVP: Add TeliProvider, SIMComProvider based on customer demand

### Risk 3: Merge Conflicts in SpeedTestResult.cs
**Probability**: High
**Impact**: Low - Easy to resolve, but time-consuming

**Mitigation**:
- **Merge Order**: Package C (Location) BEFORE Package D (Cellular)
- **Frequent Rebasing**: Every 2 days against main
- **Code Reviews**: Catch conflicts early
- **Worst Case**: 2-3 hours to resolve (built into timeline)

### Risk 4: .NET 8 Runtime Size on Constrained Devices
**Probability**: Low
**Impact**: Medium - Some devices have < 100MB storage

**Mitigation**:
- Current 3.1MB framework-dependent deploy already acceptable
- Offer both: framework-dependent (3MB) + self-contained (60MB)
- Week 6: Investigate .NET 8 AOT if needed (1.5MB potential)
- Pi Zero (512MB RAM) is most constrained target - validated in Week 7

### Risk 5: GPS Auto-Detection Complexity
**Probability**: Medium
**Impact**: Low - Manual input works as fallback

**Mitigation**:
- **MVP**: Manual input ONLY (`--location "lat,lon"`)
- **Post-MVP**: Auto-detection from gpsd as enhancement
- Manual input covers 80% of use cases (static deployments)

---

## 8. Success Metrics

### Technical Metrics (MVP Release - Week 8)
- ✅ Binary size: < 5MB (current: 3.1MB)
- ✅ Memory usage: < 50MB RAM (current: ~35MB)
- ✅ Data usage: < 1MB per micro test (target: 450KB)
- ✅ Test coverage: > 80% (current: ~85%)
- ✅ Build time: < 5 minutes
- ✅ ARM32 + ARM64 builds: Both working

### Business Metrics (12 weeks post-MVP)
- 🎯 **5-10 pilot customers** using NetPace IoT
- 🎯 **500+ automated tests** running in production
- 🎯 **2× GitHub star growth** (current: ~3/week)
- 🎯 **1000+ NuGet downloads/month** for NetPace.Core

### Customer Validation
**3 of 5 pilot customers report**:
- "NetPace IoT saved us 10+ hours/week in troubleshooting"
- "Data usage under 1MB per test is a game-changer"
- "Cellular signal metrics helped identify coverage gaps"

---

## 9. Critical Files for Implementation

### Most Critical (Modified by Multiple Packages)
1. **`src/NetPace.Core/SpeedTestResult.cs`**
   - Modified by: Package C (Location), Package D (Cellular)
   - Risk: Merge conflict hotspot
   - Current: 17 lines (very simple)
   - Add: `Location?` and `CellularMetrics?` optional properties

2. **`src/NetPace.Console/Commands/SpeedTestCommandSettings.cs`**
   - Modified by: ALL packages (A, B, C, D)
   - Risk: Highest merge conflict probability
   - Current: 138 lines with 60+ options
   - Add: `--profile`, `--min-download`, `--location`, `--cellular`

3. **`src/NetPace.Core/Clients/Ookla/OoklaSpeedtestSettings.cs`**
   - Modified by: Package A (Presets)
   - Current: 47 lines (clean record design)
   - Add: Static `Presets` class

4. **`src/NetPace.Console/ConsoleWriters/CSVConsoleWriter.cs`**
   - Modified by: Package C (Location), Package D (Cellular)
   - Critical for IoT (CSV → database ingestion)
   - Add: Location and cellular columns

5. **`src/NetPace.Core/ISpeedTestService.cs`**
   - Modified by: NONE (no changes needed!)
   - Current: 142 lines (comprehensive interface)
   - Architecture integrity maintained

---

## 10. Post-MVP Roadmap (Week 9-12+)

### Tier 2 Features (Based on Customer Feedback)
1. **Multi-Carrier Testing** - SIM swap automation (hardware-dependent)
2. **Coverage Mapping** - Web UI for visualizing test results
3. **Batch Testing Orchestration** - Test 50-100 devices in parallel
4. **Advanced Power Profiling** - Battery consumption analysis
5. **5G NR-IoT Support** - Extend beyond LTE-M/NB-IoT

### NuGet Package Evolution
- **NetPace.Core.CellularProviders** v1.0 - Publish separately
- **NetPace.Core.LocationProviders** v1.0 - GPS abstraction library
- Maintain backward compatibility with NetPace.Core 1.x

### Platform Expansion
- Windows IoT Core support
- Azure IoT Edge integration
- AWS Greengrass integration

---

## 11. References & Research

### Market Research
- Fleet management market: [oxmaint.com](https://www.oxmaint.com/blog/post/iot-fleet-management-use-cases-2025)
- Cellular IoT technologies: [u-blox LTE-M](https://www.u-blox.com/en/technologies/lte-cat-m), [Qorvo IoT Ecosystem](https://www.qorvo.com/design-hub/blog/how-nb-iot-and-lte-m-fit-into-iot-ecosystem-future-of-cellular-iot)
- IoT connectivity: [Cisco IoT Fleet Management](https://www.cisco.com/c/en/us/solutions/collateral/internet-of-things/iot-fleet-intelligent-cellular-connect-uc.html)

### Technical References
- AT Commands: [M2MSupport](https://m2msupport.net/m2msupport/atcsq-signal-quality/), [MikroTik BG77](https://help.mikrotik.com/docs/display/UM/BG77+modem+AT+commands)
- Signal metrics: [POYNTING Signal Strength Cheat Sheet](https://poynting.tech/articles/antenna-faq/signal-strength-measure-rsrp-rsrq-and-sinr-reference-for-lte-cheat-sheet/)
- .NET 8 AOT: [Microsoft Native AOT](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- QEMU ARM: [QEMU ARM Documentation](https://www.qemu.org/docs/master/system/target-arm.html)

### IoT Domain Research
- Edge computing: [Java Code Geeks Edge-to-Cloud](https://www.javacodegeeks.com/2025/03/edge-to-cloud-data-synchronization-implementing-data-pipelines.html)
- Cellular IoT speeds: [Monogoto True Speed of Cellular IoT](https://monogoto.io/2022/12/22/the-true-speed-of-cellular-iot/)
- Network testing: [Rohde & Schwarz NB-IoT/LTE-M](https://www.rohde-schwarz.com/us/solutions/critical-infrastructure/mobile-network-testing/expertise/nb-iot/nb-iot-lte-m-network-testing_231984.html)

---

## 12. Implementation Readiness Checklist

**Before starting implementation, ensure**:
- [ ] All stakeholders reviewed and approved this plan
- [ ] Hardware procurement initiated (Pi Zero, Quectel modem, SIM card)
- [ ] GitHub project board structure defined (create during kickoff)
- [ ] Git worktree strategy communicated to team
- [ ] Development environments set up (.NET 8, Linux VMs, QEMU)
- [ ] Test hardware test bench location identified
- [ ] Pilot customer list identified (5-10 fleet managers or device manufacturers)
- [ ] Success metrics baseline established (current NuGet downloads, GitHub stars)

---

## Summary

NetPace for IoT is **architecturally ready** for this expansion. The zero-dependency Core library, interface-driven design, and progressive enhancement pattern make this a **feature expansion, not a rewrite**.

**MVP delivers critical value**:
- ✅ 450KB micro tests (vs 100MB+ current)
- ✅ Automated monitoring via cron/systemd
- ✅ GPS location tracking for fleet deployment
- ✅ Cellular signal metrics for connectivity diagnostics

**Timeline**: 6-8 weeks to MVP, 12 weeks to pilot customer validation

**Investment**: ~$300 hardware (user-approved), leverage existing codebase

**Market Opportunity**: $36.3B by 2034, addressing 74% IoT pilot failure rate

**Let's build NetPace for IoT! 🚀**
