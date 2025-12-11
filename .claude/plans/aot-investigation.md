# NetPace AOT Compilation Investigation

**Goal**: Determine feasibility of AOT compilation for NetPace IoT deployment across all target platforms.

**Critical Question**: Can we use Spectre.Console.Cli with AOT, or do we need an alternative approach?

**Test Platforms**:
- `win-x64` (Windows x64)
- `linux-x64` (Linux x64)
- `linux-arm` (Linux ARM32 - Raspberry Pi Zero, older devices)
- `linux-arm64` (Linux ARM64 - Raspberry Pi 4+, modern devices)

---

## Background

### AOT Compilation Benefits for IoT
- **Self-contained**: No .NET runtime installation needed (saves ~200MB)
- **Faster startup**: Pre-compiled to native code
- **Smaller total footprint**: Runtime optimized and included in binary
- **Better for constrained devices**: Raspberry Pi Zero, embedded gateways

### Current NetPace Dependencies
```xml
<PackageReference Include="Spectre.Console" Version="0.49.1" />
<PackageReference Include="Spectre.Console.Cli" Version="0.49.1" />
```

**Known AOT Compatibility**:
- ✅ **Spectre.Console**: AOT compatible as of v0.48+
- ⚠️ **Spectre.Console.Cli**: NOT fully AOT compatible - uses reflection for command attribute parsing

---

## Test 1: Baseline - Current Build Sizes

**Purpose**: Establish baseline before AOT optimization

### Commands (All Platforms)

```bash
cd C:\Users\info\Documents\Repos\NetPace

# Framework-dependent (requires .NET runtime on target)
dotnet publish src/NetPace.Console -c Release -r win-x64 --no-self-contained -o publish/test1/baseline-win-x64
dotnet publish src/NetPace.Console -c Release -r linux-x64 --no-self-contained -o publish/test1/baseline-linux-x64
dotnet publish src/NetPace.Console -c Release -r linux-arm --no-self-contained -o publish/test1/baseline-linux-arm
dotnet publish src/NetPace.Console -c Release -r linux-arm64 --no-self-contained -o publish/test1/baseline-linux-arm64

# Self-contained (includes full runtime, no AOT)
dotnet publish src/NetPace.Console -c Release -r win-x64 --self-contained -o publish/test1/selfcontained-win-x64
dotnet publish src/NetPace.Console -c Release -r linux-x64 --self-contained -o publish/test1/selfcontained-linux-x64
dotnet publish src/NetPace.Console -c Release -r linux-arm --self-contained -o publish/test1/selfcontained-linux-arm
dotnet publish src/NetPace.Console -c Release -r linux-arm64 --self-contained -o publish/test1/selfcontained-linux-arm64
```

### Measure Sizes

```bash
# Windows (PowerShell)
Get-ChildItem publish/test1 -Recurse | Measure-Object -Property Length -Sum | Select-Object @{Name="Size(MB)";Expression={[math]::Round($_.Sum/1MB,2)}}

# Or use tree command
tree /F publish/test1 > test1-results.txt

# Linux/WSL
du -sh publish/test1/*/
ls -lh publish/test1/*/NetPace*
```

### Results Table

| Configuration | win-x64 | linux-x64 | linux-arm | linux-arm64 |
|---------------|---------|-----------|-----------|-------------|
| **Framework-dependent** | ___ MB | ___ MB | ___ MB | ___ MB |
| **Self-contained** | ___ MB | ___ MB | ___ MB | ___ MB |

**Expected**:
- Framework-dependent: ~3-5 MB (just app, no runtime)
- Self-contained: ~60-80 MB (includes full .NET 8 runtime)

---

## Test 2: AOT Without Trimming

**Purpose**: Test if Spectre.Console.Cli works with AOT at all

### Setup

**Backup original csproj**:
```bash
cp src/NetPace.Console/NetPace.Console.csproj src/NetPace.Console/NetPace.Console.csproj.backup
```

**Add AOT configuration** to `src/NetPace.Console/NetPace.Console.csproj`:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <InvariantGlobalization>false</InvariantGlobalization>
</PropertyGroup>
```

### Commands (All Platforms)

```bash
# Build with AOT (no trimming yet)
dotnet publish src/NetPace.Console -c Release -r win-x64 -o publish/test2/aot-win-x64 2>&1 | tee publish/test2/build-win-x64.log
dotnet publish src/NetPace.Console -c Release -r linux-x64 -o publish/test2/aot-linux-x64 2>&1 | tee publish/test2/build-linux-x64.log
dotnet publish src/NetPace.Console -c Release -r linux-arm -o publish/test2/aot-linux-arm 2>&1 | tee publish/test2/build-linux-arm.log
dotnet publish src/NetPace.Console -c Release -r linux-arm64 -o publish/test2/aot-linux-arm64 2>&1 | tee publish/test2/build-linux-arm64.log
```

### Analyze Warnings

```bash
# Extract all IL warnings from build logs
grep -i "warning IL" publish/test2/*.log | sort | uniq > publish/test2/warnings-summary.txt

# Common warnings to look for:
# - IL2026: Using member with 'RequiresUnreferencedCodeAttribute'
# - IL3050: Using member with 'RequiresDynamicCodeAttribute'
# - IL2111: Method with parameters or return type that have 'DynamicallyAccessedMembersAttribute'
```

### Measure Sizes

```bash
du -sh publish/test2/*/
ls -lh publish/test2/*/NetPace*
```

### Results Table

| Platform | Binary Size | Build Status | Warning Count | Key Warnings |
|----------|-------------|--------------|---------------|--------------|
| **win-x64** | ___ MB | ✅/❌ | ___ | ___ |
| **linux-x64** | ___ MB | ✅/❌ | ___ | ___ |
| **linux-arm** | ___ MB | ✅/❌ | ___ | ___ |
| **linux-arm64** | ___ MB | ✅/❌ | ___ | ___ |

**Expected**:
- Binary size: ~10-20 MB (AOT compiled, runtime included, no trimming)
- Warnings: Likely many IL2026/IL3050 warnings about Spectre.Console.Cli reflection usage
- Build status: Probably succeeds with warnings

---

## Test 3: AOT With Trimming (Aggressive)

**Purpose**: Achieve smallest binary size, identify what breaks

### Setup

**Update AOT configuration** in `src/NetPace.Console/NetPace.Console.csproj`:
```xml
<PropertyGroup>
  <PublishAot>true</PublishAot>
  <PublishTrimmed>true</PublishTrimmed>
  <TrimMode>link</TrimMode>
  <InvariantGlobalization>false</InvariantGlobalization>
  <!-- Optimize for size -->
  <IlcOptimizationPreference>Size</IlcOptimizationPreference>
  <IlcGenerateStackTraceData>false</IlcGenerateStackTraceData>
</PropertyGroup>
```

### Commands (All Platforms)

```bash
# Build with AOT + aggressive trimming
dotnet publish src/NetPace.Console -c Release -r win-x64 -o publish/test3/aot-trimmed-win-x64 2>&1 | tee publish/test3/build-win-x64.log
dotnet publish src/NetPace.Console -c Release -r linux-x64 -o publish/test3/aot-trimmed-linux-x64 2>&1 | tee publish/test3/build-linux-x64.log
dotnet publish src/NetPace.Console -c Release -r linux-arm -o publish/test3/aot-trimmed-linux-arm 2>&1 | tee publish/test3/build-linux-arm.log
dotnet publish src/NetPace.Console -c Release -r linux-arm64 -o publish/test3/aot-trimmed-linux-arm64 2>&1 | tee publish/test3/build-linux-arm64.log
```

### Analyze Warnings

```bash
# Extract trim warnings
grep -i "warning IL" publish/test3/*.log | sort | uniq > publish/test3/warnings-summary.txt

# Look for trimming-specific warnings:
# - IL2104: Assembly marked with 'TrimmerDefaultAction' but not all members are preserved
# - IL2087: Target parameter uses different annotation than source
```

### Measure Sizes

```bash
du -sh publish/test3/*/
ls -lh publish/test3/*/NetPace*
```

### Results Table

| Platform | Binary Size | Build Status | Warning Count | Trim Warnings |
|----------|-------------|--------------|---------------|---------------|
| **win-x64** | ___ MB | ✅/❌ | ___ | ___ |
| **linux-x64** | ___ MB | ✅/❌ | ___ | ___ |
| **linux-arm** | ___ MB | ✅/❌ | ___ | ___ |
| **linux-arm64** | ___ MB | ✅/❌ | ___ | ___ |

**Expected**:
- Binary size: ~3-8 MB (aggressive trimming removes unused code)
- Warnings: Many more warnings than Test 2
- Build status: May fail if trimming removes required types
- **High probability of breaking Spectre.Console.Cli**

---

## Test 4: Functional Validation

**Purpose**: Verify AOT-compiled binaries actually work

### Test Commands

Create test script `test-netpace.ps1` (Windows) or `test-netpace.sh` (Linux):

```bash
#!/bin/bash
BINARY=$1
PLATFORM=$2

echo "Testing $BINARY on $PLATFORM"
echo "================================"

# Test 1: Version
echo "Test: --version"
$BINARY --version
echo "Result: $?"
echo ""

# Test 2: Help
echo "Test: --help"
$BINARY --help
echo "Result: $?"
echo ""

# Test 3: Servers help
echo "Test: servers --help"
$BINARY servers --help
echo "Result: $?"
echo ""

# Test 4: List servers (if working)
echo "Test: servers (actual)"
$BINARY servers
echo "Result: $?"
echo ""

# Test 5: Speed test (minimal)
echo "Test: speed test (minimal)"
$BINARY --no-upload --downloadsize 1 --no-latency
echo "Result: $?"
```

### Run Tests

```bash
# Test 2 (AOT no trimming)
./test-netpace.sh publish/test2/aot-linux-x64/NetPace "AOT-linux-x64" > test4-aot-linux-x64.log
./test-netpace.sh publish/test2/aot-linux-arm64/NetPace "AOT-linux-arm64" > test4-aot-linux-arm64.log

# Test 3 (AOT with trimming)
./test-netpace.sh publish/test3/aot-trimmed-linux-x64/NetPace "AOT-Trimmed-linux-x64" > test4-trimmed-linux-x64.log
./test-netpace.sh publish/test3/aot-trimmed-linux-arm64/NetPace "AOT-Trimmed-linux-arm64" > test4-trimmed-linux-arm64.log

# Windows (PowerShell)
.\test-netpace.ps1 publish\test2\aot-win-x64\NetPace.exe "AOT-win-x64"
.\test-netpace.ps1 publish\test3\aot-trimmed-win-x64\NetPace.exe "AOT-Trimmed-win-x64"

# ARM32 with QEMU (if available)
qemu-arm-static publish/test2/aot-linux-arm/NetPace --version
qemu-arm-static publish/test3/aot-trimmed-linux-arm/NetPace --version
```

### Results Table

| Test Command | AOT win-x64 | AOT linux-x64 | AOT linux-arm | AOT linux-arm64 |
|--------------|-------------|---------------|---------------|-----------------|
| `--version` | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| `--help` | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| `servers --help` | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| `servers` (actual) | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| Speed test (minimal) | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |

| Test Command | Trimmed win-x64 | Trimmed linux-x64 | Trimmed linux-arm | Trimmed linux-arm64 |
|--------------|-----------------|-------------------|-------------------|---------------------|
| `--version` | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| `--help` | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| `servers --help` | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| `servers` (actual) | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |
| Speed test (minimal) | ✅/❌ | ✅/❌ | ✅/❌ | ✅/❌ |

**Expected Issues**:
- `--help` may fail if command attributes aren't preserved (reflection)
- Command parsing may throw `MissingMethodException` or `TypeLoadException`
- Settings validation may break if attributes are trimmed

---

## Comprehensive Comparison Table

### Binary Size Comparison

| Configuration | win-x64 | linux-x64 | linux-arm | linux-arm64 |
|---------------|---------|-----------|-----------|-------------|
| **Baseline (framework-dep)** | ___ MB | ___ MB | ___ MB | ___ MB |
| **Self-contained** | ___ MB | ___ MB | ___ MB | ___ MB |
| **AOT (no trim)** | ___ MB | ___ MB | ___ MB | ___ MB |
| **AOT + Trimming** | ___ MB | ___ MB | ___ MB | ___ MB |
| **Size Reduction** | ___% | ___% | ___% | ___% |

### Warning Count Comparison

| Platform | AOT Warnings | AOT+Trim Warnings | Critical Warnings |
|----------|--------------|-------------------|-------------------|
| **win-x64** | ___ | ___ | ___ |
| **linux-x64** | ___ | ___ | ___ |
| **linux-arm** | ___ | ___ | ___ |
| **linux-arm64** | ___ | ___ | ___ |

### Functionality Comparison

| Configuration | `--help` Works | `servers` Works | Speed Test Works | Overall Status |
|---------------|----------------|-----------------|------------------|----------------|
| **AOT win-x64** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **AOT linux-x64** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **AOT linux-arm** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **AOT linux-arm64** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **Trimmed win-x64** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **Trimmed linux-x64** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **Trimmed linux-arm** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |
| **Trimmed linux-arm64** | ✅/❌ | ✅/❌ | ✅/❌ | ✅/⚠️/❌ |

---

## Alternative Approaches If Spectre.Console.Cli Fails

### Option 1: System.CommandLine (Microsoft, AOT-Compatible)

**Status**: [GA as of .NET 9](https://github.com/dotnet/command-line-api), AOT-compatible

**Pros**:
- ✅ Microsoft-maintained, modern API
- ✅ Full AOT support
- ✅ Fluent syntax, similar to Spectre.Console.Cli
- ✅ Built-in help generation

**Cons**:
- ⚠️ Different API than Spectre.Console.Cli (requires rewrite)
- ⚠️ Requires .NET 8+ (we're already on .NET 8)

**Example**:
```csharp
using System.CommandLine;

var rootCommand = new RootCommand("NetPace - Network speed tester");

var profileOption = new Option<string>(
    aliases: new[] { "--profile", "-p" },
    description: "Test profile (micro, standard)",
    getDefaultValue: () => "standard");

rootCommand.AddOption(profileOption);
rootCommand.SetHandler((profile) => {
    // Execute test
}, profileOption);

return await rootCommand.InvokeAsync(args);
```

### Option 2: Manual Command Parsing (Fully AOT-Compatible)

**Pros**:
- ✅ Complete control
- ✅ Zero dependencies
- ✅ Smallest possible binary
- ✅ 100% AOT compatible

**Cons**:
- ⚠️ Most work to implement
- ⚠️ Lose automatic help generation
- ⚠️ Need validation logic

**Example**:
```csharp
public class Args
{
    public static Args Parse(string[] args)
    {
        var result = new Args();
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i] == "--profile" && i + 1 < args.Length)
                result.Profile = args[++i];
            else if (args[i] == "--location" && i + 1 < args.Length)
                result.Location = args[++i];
            // ...
        }
        return result;
    }
}
```

### Option 3: Hybrid Approach (Desktop + IoT)

**Strategy**: Separate projects with different CLI frameworks

```
src/
├── NetPace.Core/           # Shared (interface-driven)
├── NetPace.Console/        # Desktop (Spectre.Console.Cli, no AOT)
└── NetPace.IoT/           # Embedded (System.CommandLine or manual, AOT)
```

**Pros**:
- ✅ Best of both worlds
- ✅ Desktop keeps rich UX
- ✅ IoT gets minimal binary

**Cons**:
- ⚠️ Maintain two CLI projects
- ⚠️ Some code duplication

---

## Investigation Execution Steps

### Preparation (5 minutes)
```bash
cd C:\Users\info\Documents\Repos\NetPace

# Create publish directories
mkdir -p publish/{test1,test2,test3,test4}

# Backup original csproj
cp src/NetPace.Console/NetPace.Console.csproj src/NetPace.Console/NetPace.Console.csproj.backup
```

### Test 1: Baseline (20 minutes)
```bash
# Run all baseline builds (8 builds total)
# Framework-dependent (4) + Self-contained (4)

# Measure and record sizes
# Fill in Test 1 results table
```

### Test 2: AOT (30 minutes)
```bash
# Add AOT config to csproj
# Run 4 AOT builds
# Collect warnings
# Measure sizes
# Fill in Test 2 results table
```

### Test 3: AOT + Trimming (30 minutes)
```bash
# Update csproj with trimming
# Run 4 trimmed builds
# Collect warnings
# Measure sizes
# Fill in Test 3 results table
```

### Test 4: Functional Testing (30 minutes)
```bash
# Create test script
# Run tests on all binaries
# Document which commands work/fail
# Fill in Test 4 results tables
```

### Analysis & Report (20 minutes)
```bash
# Fill comprehensive comparison tables
# Calculate size reductions
# Identify critical warnings
# Document broken functionality
# Make recommendation
```

**Total Time: ~2 hours**

---

## Decision Tree

```
Can we AOT compile NetPace with Spectre.Console.Cli?
│
├─ ✅ Yes, all tests pass
│   └─ Recommendation: Use AOT for all IoT builds
│       - Binary size: ~5-10 MB (excellent for IoT)
│       - No runtime installation needed
│       - Proceed with current architecture
│
├─ ⚠️ Partially - Basic commands work, --help broken
│   └─ Recommendation: Add DynamicallyAccessedMembers attributes
│       - Try to fix reflection warnings
│       - Simplify command structure for IoT
│       - Consider System.CommandLine for future
│
└─ ❌ No, critical functionality broken
    └─ Recommendation: Switch CLI framework
        - Option A: System.CommandLine (preferred)
        - Option B: Manual parsing (smallest binary)
        - Option C: Hybrid (separate NetPace.IoT project)
```

---

## Final Recommendation Template

After completing all tests, fill this section:

### Summary

- **AOT Compilation Status**: ✅ Works / ⚠️ Partial / ❌ Broken
- **Smallest Binary Size Achieved**: ___ MB (platform: ___)
- **Critical Warnings**: ___ IL2026, ___ IL3050, ___ IL2104
- **Functional Completeness**: ___% of commands working

### Recommended Approach for IoT

[Insert recommendation here based on test results]

### Impact on netpace-iot-mvp.md Plan

[Describe any changes needed to the main IoT implementation plan]

---

## Cleanup After Investigation

```bash
# Restore original csproj
cp src/NetPace.Console/NetPace.Console.csproj.backup src/NetPace.Console/NetPace.Console.csproj

# Keep publish directory for reference
# Can be deleted later or gitignored
echo "publish/" >> .gitignore
```

---

## References

- [.NET 8 Native AOT Deployment](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/)
- [Preparing .NET Libraries for Trimming](https://learn.microsoft.com/en-us/dotnet/core/deploying/trimming/prepare-libraries-for-trimming)
- [Introduction to AOT Warnings (IL2026, IL3050, etc.)](https://learn.microsoft.com/en-us/dotnet/core/deploying/native-aot/fixing-warnings)
- [Spectre.Console AOT Support](https://github.com/spectreconsole/spectre.console/issues/1304)
- [System.CommandLine Documentation](https://learn.microsoft.com/en-us/dotnet/standard/commandline/)
- [QEMU User Emulation](https://www.qemu.org/docs/master/user/main.html)
