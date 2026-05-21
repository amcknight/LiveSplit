# SMW Counters Unification Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace `LiveSplit.SmwDeathCounter` and the in-progress `LiveSplit.SmwMoonCounter` with a single `LiveSplit.SmwCounters` component that shares one `SnesEmu`, multiplexes counters through an `ISmwCounter` abstraction, and renders enabled counters as one horizontal row.

**Architecture:** New component project under `components/LiveSplit.SmwCounters/` (plain folder, like the existing `PressCounter`/`SmwDeathCounter` — neither is a real submodule). Internal `ISmwCounter` interface with `DeathCounter` and `MoonCounter` implementations. One `SnesEmu` (refactored to expose an `ISnesMemory` interface for testability), one poll timer, one settings UserControl. Old two SMW component projects deleted from disk and from `LiveSplit.sln`.

**Tech Stack:** .NET Framework 4.8.1, .NET 8.0 SDK build, C# 12, WinForms, xUnit (in `test/LiveSplit.Tests`).

**Spec:** `docs/superpowers/specs/2026-05-21-smw-counters-unification-design.md`

**Precondition:** This plan ports `SnesEmu`, `Offsets`, `DeathCounter` polling logic, and `MoonCounter` polling logic *verbatim* into the new component. If the existing `components/LiveSplit.SmwDeathCounter/` working tree has uncommitted local modifications (the session-start `git status` showed one), discard them before starting — the same content is re-introduced under the new namespace in Tasks 2, 3, 6, and 7. Run:

```powershell
git checkout -- components\LiveSplit.SmwDeathCounter
```

(Or `git stash` if you want to keep them around for reference.)

---

## File Structure

**New files (all under `components/LiveSplit.SmwCounters/`):**
- `Directory.Build.props` — boilerplate that imports parent + per-project props.
- `props/LiveSplit.SmwCounters.Paths.props` — `RootPath`/`SrcPath`/`TestPath` defaults for standalone builds.
- `props/LiveSplit.SmwCounters.props` — `LangVersion`, `Nullable`, `EnforceCodeStyleInBuild`, `UseArtifactsOutput`.
- `src/LiveSplit.SmwCounters/LiveSplit.SmwCounters.csproj` — WinForms component DLL targeting net4.8.1.
- `src/LiveSplit.SmwCounters/Snes/Offsets.cs` — verbatim port of WRAM offset/pointer tables.
- `src/LiveSplit.SmwCounters/Snes/ISnesMemory.cs` — small read surface for tests.
- `src/LiveSplit.SmwCounters/Snes/SnesEmu.cs` — port of emulator-detection + WRAM read, implements `ISnesMemory`.
- `src/LiveSplit.SmwCounters/Counters/ISmwCounter.cs` — counter contract.
- `src/LiveSplit.SmwCounters/Counters/DeathCounter.cs` — Mario-animation edge counter.
- `src/LiveSplit.SmwCounters/Counters/MoonCounter.cs` — moon-byte edge counter with per-level/per-room dedupe.
- `src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentSettings.cs` — UserControl + XML round-trip.
- `src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponent.cs` — `IComponent`.
- `src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentFactory.cs` — factory with `[assembly: ComponentFactory(...)]`.

**New test files (under `test/LiveSplit.Tests/`):**
- `Components/SmwCounters/FakeSnesMemory.cs` — scripted in-memory `ISnesMemory`.
- `Components/SmwCounters/DeathCounterTests.cs`
- `Components/SmwCounters/MoonCounterTests.cs`
- `Components/SmwCounters/SmwCountersSettingsTests.cs`

**Files to modify:**
- `LiveSplit.sln` — add new project, remove `LiveSplit.SmwDeathCounter` and `LiveSplit.SmwMoonCounter`.
- `test/LiveSplit.Tests/LiveSplit.Tests.csproj` — add `ProjectReference` to the new component.

**Files/folders to delete:**
- `components/LiveSplit.SmwDeathCounter/` (tracked — `git rm -r`).
- `components/LiveSplit.SmwMoonCounter/` (untracked — plain `rm -rf`).

---

## Task 1: Scaffold the new project

**Files:**
- Create: `components/LiveSplit.SmwCounters/Directory.Build.props`
- Create: `components/LiveSplit.SmwCounters/props/LiveSplit.SmwCounters.Paths.props`
- Create: `components/LiveSplit.SmwCounters/props/LiveSplit.SmwCounters.props`
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/LiveSplit.SmwCounters.csproj`

- [ ] **Step 1: Create Directory.Build.props**

Path: `components/LiveSplit.SmwCounters/Directory.Build.props`

```xml
<Project>

  <!-- Imports `Directory.Build.props` from the above directory, if it exists. -->
  <!-- If it does not, the properties `LsSrcPath`, `LsLibPath`, and `ComponentsPath` must be provided via the command line. -->
  <!-- Example: `dotnet build -p:LsSrcPath=path/to/LiveSplit/src` -->

  <Import Project="$([MSBuild]::GetPathOfFileAbove(Directory.Build.props, $(MSBuildThisFileDirectory)..))" />
  <Import Project="$(MSBuildThisFileDirectory)props\*.props" />

</Project>
```

- [ ] **Step 2: Create Paths.props**

Path: `components/LiveSplit.SmwCounters/props/LiveSplit.SmwCounters.Paths.props`

```xml
<Project>

  <PropertyGroup>
    <RootPath>$(MSBuildThisFileDirectory)..</RootPath>

    <SrcPath>$(RootPath)\src</SrcPath>
    <TestPath>$(RootPath)\test</TestPath>
  </PropertyGroup>

</Project>
```

- [ ] **Step 3: Create main props file**

Path: `components/LiveSplit.SmwCounters/props/LiveSplit.SmwCounters.props`

```xml
<Project>

  <PropertyGroup>
    <LangVersion>12</LangVersion>

    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <Nullable>disable</Nullable>
  </PropertyGroup>

  <PropertyGroup>
    <UseArtifactsOutput>true</UseArtifactsOutput>
  </PropertyGroup>

</Project>
```

- [ ] **Step 4: Create the component csproj**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/LiveSplit.SmwCounters.csproj`

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <RootNamespace>LiveSplit</RootNamespace>
    <UseWindowsForms>true</UseWindowsForms>
    <TargetFramework>net4.8.1</TargetFramework>

    <EnableDynamicLoading>true</EnableDynamicLoading>
  </PropertyGroup>

  <ItemGroup>
    <ProjectReference Include="$(LsSrcPath)\LiveSplit.Core\LiveSplit.Core.csproj" Private="false" ExcludeAssets="runtime" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="PolySharp" Version="1.14.1" PrivateAssets="all" />
  </ItemGroup>

</Project>
```

- [ ] **Step 5: Verify the project file parses (it has no code yet, so this is a syntactic check)**

Run from the repo root:
```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds with zero source files. (Will produce an empty assembly.)

- [ ] **Step 6: Commit**

```bash
git add components/LiveSplit.SmwCounters/Directory.Build.props \
        components/LiveSplit.SmwCounters/props/ \
        components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/LiveSplit.SmwCounters.csproj
git commit -m "Scaffold LiveSplit.SmwCounters project"
```

---

## Task 2: Port the Offsets table

**Files:**
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/Offsets.cs`

This is a verbatim copy of `components/LiveSplit.SmwDeathCounter/src/LiveSplit.SmwDeathCounter/Snes/Offsets.cs` with only the namespace changed. Use it as-is — the offset research is owned by the kaizosplits project and we want a single source.

- [ ] **Step 1: Create Offsets.cs**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/Offsets.cs`

```csharp
using System.Collections.Generic;

using LiveSplit.ComponentUtil;

// Offset tables ported verbatim from kaizosplits Components/SMW/SMW/SNES/Offset.cs.
// All offset research credit belongs to that project.

namespace LiveSplit.SmwCounters.Snes;

internal static class Offsets
{
    // Process-name candidates to scan for, in priority order.
    public static readonly string[] KnownProcessNames =
    {
        "retroarch",
        "snes9x",
        "snes9x-x64",
        "snes9x-rr",
        "bsnes",
        "higan",
        "emuhawk",
    };

    // emulator EXE MainModule.ModuleMemorySize -> version label.
    public static readonly Dictionary<int, string> Version = new()
    {
        { 15675392, "1.9.4"  }, // retroarch
        { 16793600, "1.16.0" }, // retroarch
        { 17264640, "1.17.0" }, // retroarch
        { 18350080, "1.21.0" }, // retroarch
        { 20008960, "1.22.2" }, // retroarch
        {  6991872, "1.57"   }, // snes9x
        {  9027584, "1.60"   }, // snes9x
        {  9158656, "1.61"   }, // snes9x
        { 10399744, "1.62.3" }, // snes9x
        { 12537856, "1.59.2" }, // snes9x x64
        { 12836864, "1.60"   }, // snes9x x64
        { 12955648, "1.61"   }, // snes9x x64
        { 29069312, "1.62"   }, // snes9x x64
        { 15474688, "1.62.3" }, // snes9x x64 (also 1.62.2)
        {  9646080, "1.60"   }, // snes9x-rr
        { 13565952, "1.60"   }, // snes9x-rr x64
        { 10096640, "107"    }, // bsnes
        { 10338304, "107.1"  }, // bsnes
        { 47230976, "107.2"  }, // bsnes (also 107.3)
        {131543040, "110"    }, // bsnes
        { 51924992, "111"    }, // bsnes
        { 52056064, "112"    }, // bsnes
        { 52477952, "115"    }, // bsnes
        { 16019456, "106"    }, // higan
        { 15360000, "106.112"}, // higan
        { 22388736, "107"    }, // higan
        { 23142400, "108"    }, // higan
        { 23166976, "109"    }, // higan
        { 23224320, "110"    }, // higan
        {  7061504, "2.3"    }, // BizHawk
        {  7249920, "2.3.1"  }, // BizHawk
        {  6938624, "2.3.2"  }, // BizHawk
    };

    // Direct WRAM base address (module + offset, computed at attach time).
    public static readonly Dictionary<string, long> Mem = new()
    {
        { "higan 106",    0x94D144 },
        { "higan 106.112",0x8AB144 },
        { "higan 107",    0xB0ECC8 },
        { "higan 108",    0xBC7CC8 },
        { "higan 109",    0xBCECC8 },
        { "higan 110",    0xBDBCC8 },
        { "bsnes 107",    0x72BECC },
        { "bsnes 107.1",  0x762F2C },
        { "bsnes 107.2",  0x765F2C },
        { "bsnes 107.3",  0x765F2C },
        { "bsnes 110",    0xA9BD5C },
        { "bsnes 111",    0xA9DD5C },
        { "bsnes 112",    0xAAED7C },
        { "bsnes 115",    0xB16D7C },
        { "emuhawk 2.3",  0x36F11500240 },
        { "emuhawk 2.3.1",0x36F11500240 },
        { "emuhawk 2.3.2",0x36F11500240 },
    };

    // Pointer chain to dereference for WRAM base.
    public static readonly Dictionary<string, DeepPointer> MemPtr = new()
    {
        { "snes9x 1.60",   new DeepPointer("snes9x.exe", 0x54DB54, 0x0) },
        { "snes9x 1.61",   new DeepPointer("snes9x.exe", 0x507BC4, 0x0) },
        { "snes9x 1.62.3", new DeepPointer("snes9x.exe",  0x12698, 0x0) },
        { "snes9x-x64 1.59.2", new DeepPointer("snes9x-x64.exe",  0x8D86F8, 0x0) },
        { "snes9x-x64 1.60",   new DeepPointer("snes9x-x64.exe",  0x8D86F8, 0x0) },
        { "snes9x-x64 1.61",   new DeepPointer("snes9x-x64.exe",  0x883158, 0x0) },
        { "snes9x-x64 1.62",   new DeepPointer("snes9x-x64.exe", 0x1758D40, 0x0) },
        { "snes9x-x64 1.62.2", new DeepPointer("snes9x-x64.exe",  0xA62390, 0x0) },
        { "snes9x-x64 1.62.3", new DeepPointer("snes9x-x64.exe",  0xA62390, 0x0) },
    };

    // RetroArch: pointer to the loaded core's filename (e.g. "snes9x_libretro.dll").
    public static readonly Dictionary<string, DeepPointer> CorePathPtr = new()
    {
        { "retroarch 1.9.4",  new DeepPointer("retroarch.exe", 0xD6A900) },
        { "retroarch 1.16.0", new DeepPointer("retroarch.exe", 0xE8F7E9) },
        { "retroarch 1.17.0", new DeepPointer("retroarch.exe", 0xEEB59A) },
        { "retroarch 1.21.0", new DeepPointer("retroarch.exe", 0xFB157C) },
        { "retroarch 1.22.2", new DeepPointer("retroarch.exe", 0x114F8B9) },
    };

    // RetroArch: pointer to the loaded core's version string.
    public static readonly Dictionary<string, DeepPointer> CoreVersionPtr = new()
    {
        { "retroarch 1.9.4",  new DeepPointer("retroarch.exe", 0xD67600) },
        { "retroarch 1.16.0", new DeepPointer("retroarch.exe", 0xE8C4E9) },
        { "retroarch 1.17.0", new DeepPointer("retroarch.exe", 0xEFD5A9) },
        { "retroarch 1.21.0", new DeepPointer("retroarch.exe", 0xFBE399) },
        { "retroarch 1.22.2", new DeepPointer("retroarch.exe", 0x1150BB9) },
    };

    // Core-DLL relative offset for WRAM base.
    public static readonly Dictionary<string, int> CoreMem = new()
    {
        { "snes9x_libretro.dll 1.62.3 ec4ebfc", 0x3BA164 },
        { "snes9x_libretro.dll 1.63 49f4845",   0x3BB164 },
        { "bsnes_libretro.dll 115",             0x7D39DC },
    };

    public static readonly Dictionary<string, DeepPointer> CoreMemPtr = new()
    {
        { "snes9x2010_libretro.dll 1.52.4 d8b10c4", new DeepPointer("retroarch.exe", 0xEF9FF8, 0x8, 0x0) },
    };
}
```

- [ ] **Step 2: Build to confirm the offsets compile against LiveSplit.Core's DeepPointer**

Run:
```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/Offsets.cs
git commit -m "Port SMW WRAM offsets tables to SmwCounters"
```

---

## Task 3: Add ISnesMemory and port SnesEmu

The counter tests need a way to feed scripted byte sequences without spawning an emulator process. Introduce a small `ISnesMemory` interface that exposes only what counters call, and have `SnesEmu` implement it.

**Files:**
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/ISnesMemory.cs`
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/SnesEmu.cs`

- [ ] **Step 1: Create ISnesMemory.cs**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/ISnesMemory.cs`

```csharp
namespace LiveSplit.SmwCounters.Snes;

internal interface ISnesMemory
{
    bool IsAttached { get; }
    bool ReadWramByte(int snesOffset, out byte value);
}
```

- [ ] **Step 2: Create SnesEmu.cs (verbatim port + ISnesMemory implementation)**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/SnesEmu.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

using LiveSplit.ComponentUtil;

// Emulator detection + WRAM-offset tables ported from kaizosplits
// (https://github.com/...kaizosplits, Components/SMW/SMW/SNES/{Emu,Offset}.cs).
// All credit for the offset research belongs to that project.

namespace LiveSplit.SmwCounters.Snes;

internal sealed class SnesEmu : ISnesMemory
{
    public string ProcessName { get; private set; }
    public string EmuVersion { get; private set; }
    public string Core { get; private set; }
    public string CoreVersion { get; private set; }
    public long WramBase { get; private set; }
    public string LastError { get; private set; }

    public Process Process { get; private set; }

    public bool IsAttached => Process != null && !Process.HasExited && WramBase != 0;

    public string Describe()
    {
        if (LastError != null && Process == null) { return LastError; }
        if (Process == null) { return "no emulator found"; }
        if (string.IsNullOrEmpty(EmuVersion)) { return $"{Process.ProcessName}: unknown version"; }
        string core = string.IsNullOrEmpty(Core) ? "" : $" / {Core} {CoreVersion}";
        return $"{ProcessName} {EmuVersion}{core}";
    }

    public void Detach()
    {
        Process = null;
        ProcessName = EmuVersion = Core = CoreVersion = null;
        WramBase = 0;
    }

    // Returns true if attached and WramBase is valid for reading.
    public bool TryAttach()
    {
        if (Process != null && Process.HasExited) { Detach(); }

        if (Process == null)
        {
            Process = FindKnownEmu();
            if (Process == null)
            {
                LastError = "no emulator found";
                return false;
            }
            ProcessName = Process.ProcessName.ToLowerInvariant();
        }

        try
        {
            if (string.IsNullOrEmpty(EmuVersion))
            {
                int size = Process.MainModuleWow64Safe().ModuleMemorySize;
                if (!Offsets.Version.TryGetValue(size, out string ver))
                {
                    LastError = $"unknown {ProcessName} build (module size {size})";
                    return false;
                }
                EmuVersion = ver;
            }

            if (ProcessName == "retroarch")
            {
                if (!ResolveRetroArchCore()) { return false; }
            }

            if (WramBase == 0)
            {
                if (!ResolveWramBase()) { return false; }
            }

            LastError = null;
            return true;
        }
        catch (Exception ex)
        {
            LastError = ex.Message;
            return false;
        }
    }

    private static Process FindKnownEmu()
    {
        foreach (string name in Offsets.KnownProcessNames)
        {
            Process p = Process.GetProcessesByName(name).FirstOrDefault();
            if (p != null) { return p; }
        }
        return null;
    }

    private bool ResolveRetroArchCore()
    {
        string emuKey = Key(ProcessName, EmuVersion);

        if (!Offsets.CorePathPtr.TryGetValue(emuKey, out DeepPointer corePathPtr))
        {
            LastError = $"no core-path pointer for '{emuKey}'";
            return false;
        }
        Core = Path.GetFileName(corePathPtr.DerefString(Process, 512) ?? "");

        if (!Offsets.CoreVersionPtr.TryGetValue(emuKey, out DeepPointer coreVerPtr))
        {
            LastError = $"no core-version pointer for '{emuKey}'";
            return false;
        }
        CoreVersion = coreVerPtr.DerefString(Process, 32) ?? "";

        if (string.IsNullOrWhiteSpace(Core)) { LastError = "no core loaded in RetroArch"; return false; }
        if (string.IsNullOrWhiteSpace(CoreVersion)) { LastError = $"no version for core '{Core}'"; return false; }
        return true;
    }

    private bool ResolveWramBase()
    {
        if (ProcessName == "retroarch")
        {
            string coreKey = Key(Core, CoreVersion);
            DeepPointer corePtr;
            if (Offsets.CoreMem.TryGetValue(coreKey, out int directOff))
            {
                corePtr = new DeepPointer(Core, directOff);
            }
            else if (!Offsets.CoreMemPtr.TryGetValue(coreKey, out corePtr))
            {
                LastError = $"no WRAM offset for core '{coreKey}'";
                return false;
            }
            if (!corePtr.DerefOffsets(Process, out IntPtr addr))
            {
                LastError = $"failed deref core '{coreKey}'";
                return false;
            }
            WramBase = addr.ToInt64();
        }
        else
        {
            string emuKey = Key(ProcessName, EmuVersion);
            if (Offsets.MemPtr.TryGetValue(emuKey, out DeepPointer ptr))
            {
                if (!ptr.DerefOffsets(Process, out IntPtr addr))
                {
                    LastError = $"failed deref '{emuKey}'";
                    return false;
                }
                WramBase = addr.ToInt64();
            }
            else if (Offsets.Mem.TryGetValue(emuKey, out long direct))
            {
                // kaizosplits ships these as already-absolute (assumes default EXE base, no ASLR).
                WramBase = direct;
            }
            else
            {
                LastError = $"no WRAM offset for '{emuKey}'";
                return false;
            }
        }

        if (WramBase == 0) { LastError = "WRAM base resolved to 0"; return false; }
        return true;
    }

    // Reads a byte from a SNES WRAM address (0x0000–0x1FFFF).
    public bool ReadWramByte(int snesOffset, out byte value)
    {
        value = 0;
        if (!IsAttached) { return false; }
        return Process.ReadValue((IntPtr)(WramBase + snesOffset), out value);
    }

    private static string Key(string a, string b) => $"{a} {b}";
}
```

- [ ] **Step 3: Build**

```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/ISnesMemory.cs \
        components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Snes/SnesEmu.cs
git commit -m "Port SnesEmu and add ISnesMemory interface"
```

---

## Task 4: Define ISmwCounter

**Files:**
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/ISmwCounter.cs`

- [ ] **Step 1: Create the interface**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/ISmwCounter.cs`

```csharp
using System.Xml;

using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.SmwCounters.Counters;

internal interface ISmwCounter
{
    // Stable serialization key. Must not change once shipped.
    string Id { get; }

    // Default short text shown when the user hasn't set a per-counter label override.
    string DefaultGlyph { get; }

    // Human-readable name shown in the settings dialog row label.
    string DefaultLabel { get; }

    int Value { get; }

    void Reset();

    // Called once per poll tick when the component is attached. The counter
    // performs its own reads and decides whether to increment Value.
    void Poll(ISnesMemory memory);

    // Save/restore counter-owned state (the current value plus any
    // counter-specific config like dedupe mode). Each counter is responsible
    // for choosing element names under `parent`.
    void SaveState(XmlDocument doc, XmlElement parent);
    void LoadState(XmlElement parent);
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/ISmwCounter.cs
git commit -m "Add ISmwCounter contract"
```

---

## Task 5: Add the FakeSnesMemory test helper

To unit-test counters without an emulator, add a scripted `ISnesMemory` to the test project. This requires the test project to reference the new component, so we wire that up first.

**Files:**
- Modify: `test/LiveSplit.Tests/LiveSplit.Tests.csproj`
- Create: `test/LiveSplit.Tests/Components/SmwCounters/FakeSnesMemory.cs`

- [ ] **Step 1: Add ProjectReference in LiveSplit.Tests.csproj**

Insert a new `<ProjectReference>` line in alphabetical order in the existing `ItemGroup`. The complete updated `ItemGroup` is:

```xml
  <ItemGroup>
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.CurrentComparison\src\LiveSplit.CurrentComparison\LiveSplit.CurrentComparison.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.Delta\src\LiveSplit.Delta\LiveSplit.Delta.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.DetailedTimer\src\LiveSplit.DetailedTimer\LiveSplit.DetailedTimer.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.ManualGameTime\src\LiveSplit.ManualGameTime\LiveSplit.ManualGameTime.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.PossibleTimeSave\src\LiveSplit.PossibleTimeSave\LiveSplit.PossibleTimeSave.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.PreviousSegment\src\LiveSplit.PreviousSegment\LiveSplit.PreviousSegment.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.RunPrediction\src\LiveSplit.RunPrediction\LiveSplit.RunPrediction.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.ScriptableAutoSplit\src\LiveSplit.ScriptableAutoSplit\LiveSplit.ScriptableAutoSplit.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.Splits\src\LiveSplit.Splits\LiveSplit.Splits.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.Subsplits\src\LiveSplit.Subsplits\LiveSplit.Subsplits.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.SumOfBest\src\LiveSplit.SumOfBest\LiveSplit.SumOfBest.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.Timer\src\LiveSplit.Timer\LiveSplit.Timer.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.TotalPlaytime\src\LiveSplit.TotalPlaytime\LiveSplit.TotalPlaytime.csproj" />
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.WorldRecord\src\LiveSplit.WorldRecord\LiveSplit.WorldRecord.csproj" />
    <ProjectReference Include="$(SrcPath)\LiveSplit.Core\LiveSplit.Core.csproj" />
  </ItemGroup>
```

The new line is between `ScriptableAutoSplit` and `Splits`:
```xml
    <ProjectReference Include="$(ComponentsPath)\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj" />
```

`InternalsVisibleTo` is **not** set up; the counter classes/`ISnesMemory` are `internal`. The fake lives inside the same assembly via `[assembly: InternalsVisibleTo("LiveSplit.Tests")]`. Add that attribute to the new component:

Create `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Properties/AssemblyInfo.cs`:

```csharp
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("LiveSplit.Tests")]
```

- [ ] **Step 2: Create FakeSnesMemory**

Path: `test/LiveSplit.Tests/Components/SmwCounters/FakeSnesMemory.cs`

```csharp
using System.Collections.Generic;

using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.Tests.Components.SmwCounters;

internal sealed class FakeSnesMemory : ISnesMemory
{
    private readonly Dictionary<int, byte> bytes = new();

    public bool IsAttached { get; set; } = true;

    public void SetByte(int offset, byte value) => bytes[offset] = value;

    public bool ReadWramByte(int snesOffset, out byte value)
    {
        if (!IsAttached)
        {
            value = 0;
            return false;
        }
        value = bytes.TryGetValue(snesOffset, out byte b) ? b : (byte)0;
        return true;
    }
}
```

- [ ] **Step 3: Build the test project**

```powershell
dotnet build test\LiveSplit.Tests\LiveSplit.Tests.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Commit**

```bash
git add test/LiveSplit.Tests/LiveSplit.Tests.csproj \
        test/LiveSplit.Tests/Components/SmwCounters/FakeSnesMemory.cs \
        components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Properties/AssemblyInfo.cs
git commit -m "Wire LiveSplit.SmwCounters into test project and add FakeSnesMemory"
```

---

## Task 6: DeathCounter with TDD

**Files:**
- Create: `test/LiveSplit.Tests/Components/SmwCounters/DeathCounterTests.cs`
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/DeathCounter.cs`

- [ ] **Step 1: Write failing tests**

Path: `test/LiveSplit.Tests/Components/SmwCounters/DeathCounterTests.cs`

```csharp
using LiveSplit.SmwCounters.Counters;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class DeathCounterTests
{
    // SMW Mario animation $7E:0071. 0x09 == dying.
    private const int Anim = 0x71;

    [Fact]
    public void NoEdgeOnFirstPollEvenIfDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x09);
        c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void IncrementsOnTransitionIntoDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00);
        c.Poll(mem);
        mem.SetByte(Anim, 0x09);
        c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void DoesNotDoubleCountWhileStillDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);
        c.Poll(mem); // still 0x09
        c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsAgainAfterLeavingAndReenteringDying()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);
        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);

        Assert.Equal(2, c.Value);
    }

    [Fact]
    public void DetachClearsPreviousSoNextAttachWontCountSpuriously()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.IsAttached = false; c.Poll(mem);
        mem.IsAttached = true;
        mem.SetByte(Anim, 0x09); c.Poll(mem);

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void ResetZeroesValueAndClearsEdgeState()
    {
        var mem = new FakeSnesMemory();
        var c = new DeathCounter();

        mem.SetByte(Anim, 0x00); c.Poll(mem);
        mem.SetByte(Anim, 0x09); c.Poll(mem);
        c.Reset();

        Assert.Equal(0, c.Value);

        // Currently 0x09 in memory; resetting should clear hasPrevious so we don't count it again next tick.
        c.Poll(mem);
        Assert.Equal(0, c.Value);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail (compilation error: DeathCounter doesn't exist)**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~DeathCounterTests"
```

Expected: FAIL — `DeathCounter` is not defined.

- [ ] **Step 3: Implement DeathCounter**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/DeathCounter.cs`

```csharp
using System.Xml;

using LiveSplit.Model;
using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.SmwCounters.Counters;

internal sealed class DeathCounter : ISmwCounter
{
    // SNES address $7E:0071 — Mario player animation. 0x09 == "dying".
    // Source: kaizosplits Watchers.cs (DiedNow => ShiftTo(playerAnimation, 9)).
    private const int PlayerAnimationOffset = 0x71;
    private const byte DyingValue = 0x09;

    private byte previousAnimation;
    private bool hasPrevious;

    public string Id => "deaths";
    public string DefaultGlyph => "💀";
    public string DefaultLabel => "Deaths";

    public int Value { get; private set; }

    public void Reset()
    {
        Value = 0;
        hasPrevious = false;
    }

    public void Poll(ISnesMemory memory)
    {
        if (!memory.IsAttached)
        {
            hasPrevious = false;
            return;
        }

        if (!memory.ReadWramByte(PlayerAnimationOffset, out byte anim))
        {
            hasPrevious = false;
            return;
        }

        if (hasPrevious && previousAnimation != DyingValue && anim == DyingValue)
        {
            Value++;
        }
        previousAnimation = anim;
        hasPrevious = true;
    }

    public void SaveState(XmlDocument doc, XmlElement parent)
    {
        SettingsHelper.CreateSetting(doc, parent, "Deaths", Value);
    }

    public void LoadState(XmlElement parent)
    {
        Value = SettingsHelper.ParseInt(parent["Deaths"], 0);
        hasPrevious = false;
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~DeathCounterTests"
```

Expected: PASS — all 6 tests pass.

- [ ] **Step 5: Commit**

```bash
git add test/LiveSplit.Tests/Components/SmwCounters/DeathCounterTests.cs \
        components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/DeathCounter.cs
git commit -m "Implement DeathCounter with edge-detection tests"
```

---

## Task 7: MoonCounter with TDD

**Files:**
- Create: `test/LiveSplit.Tests/Components/SmwCounters/MoonCounterTests.cs`
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/MoonCounter.cs`

- [ ] **Step 1: Write failing tests**

Path: `test/LiveSplit.Tests/Components/SmwCounters/MoonCounterTests.cs`

```csharp
using System.Xml;

using LiveSplit.SmwCounters.Counters;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class MoonCounterTests
{
    // SMW WRAM addresses used by MoonCounter (matches kaizosplits Memory.cs).
    private const int Moons = 0x13C5; // # of 3-up moons collected, per scene
    private const int Level = 0x13BF; // translevel number
    private const int Room  = 0x010B; // sublevel within current level

    private static void Set(FakeSnesMemory m, byte moons, byte level, byte room)
    {
        m.SetByte(Moons, moons);
        m.SetByte(Level, level);
        m.SetByte(Room,  room);
    }

    [Fact]
    public void IncrementsOnRisingMoonByte()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter();

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void DedupesAcrossRoomsInPerLevelMode()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter(); // DedupePerRoom defaults to false

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem); // count once for level 1
        Set(mem, 0, 1, 2); c.Poll(mem); // moved to room 2, moon byte resets
        Set(mem, 1, 1, 2); c.Poll(mem); // another moon byte rise in same level

        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void CountsEachRoomInPerRoomMode()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter { DedupePerRoom = true };

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);
        Set(mem, 0, 1, 2); c.Poll(mem);
        Set(mem, 1, 1, 2); c.Poll(mem);

        Assert.Equal(2, c.Value);
    }

    [Fact]
    public void CountsAcrossDifferentLevelsInPerLevelMode()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter();

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem); // level 1
        Set(mem, 0, 2, 0); c.Poll(mem);
        Set(mem, 1, 2, 0); c.Poll(mem); // level 2

        Assert.Equal(2, c.Value);
    }

    [Fact]
    public void DoesNotIncrementOnFirstPoll()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter();

        Set(mem, 1, 1, 0); c.Poll(mem); // first poll, even if moons==1, no edge yet

        Assert.Equal(0, c.Value);
    }

    [Fact]
    public void ResetClearsValueAndDedupeMemory()
    {
        var mem = new FakeSnesMemory();
        var c = new MoonCounter();

        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);
        c.Reset();
        Assert.Equal(0, c.Value);

        // After reset, the same level should be eligible again.
        Set(mem, 0, 1, 0); c.Poll(mem);
        Set(mem, 1, 1, 0); c.Poll(mem);
        Assert.Equal(1, c.Value);
    }

    [Fact]
    public void StateRoundTripsThroughXml()
    {
        var mem = new FakeSnesMemory();
        var a = new MoonCounter { DedupePerRoom = true };
        Set(mem, 0, 1, 0); a.Poll(mem);
        Set(mem, 1, 1, 0); a.Poll(mem);

        var doc = new XmlDocument();
        var parent = doc.CreateElement("CounterState");
        doc.AppendChild(parent);
        a.SaveState(doc, parent);

        var b = new MoonCounter();
        b.LoadState(parent);

        Assert.Equal(1, b.Value);
        Assert.True(b.DedupePerRoom);
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~MoonCounterTests"
```

Expected: FAIL — `MoonCounter` is not defined.

- [ ] **Step 3: Implement MoonCounter**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/MoonCounter.cs`

```csharp
using System.Collections.Generic;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.SmwCounters.Counters;

internal sealed class MoonCounter : ISmwCounter
{
    // SNES WRAM addresses (from kaizosplits Memory.cs).
    private const int MoonCounterOffset = 0x13C5; // # of 3-up moons collected, per scene
    private const int LevelNumOffset    = 0x13BF; // translevel number
    private const int RoomNumOffset     = 0x010B; // sublevel within current level

    private byte previousMoon;
    private bool hasPrevious;

    // Keys (level, or level+room) where a moon has been counted this session.
    private readonly HashSet<int> countedKeys = new();

    public string Id => "moons";
    public string DefaultGlyph => "🌙";
    public string DefaultLabel => "Moons";

    public int Value { get; private set; }

    // false => count one moon per translevel
    // true  => count one moon per (translevel, sublevel)
    public bool DedupePerRoom { get; set; }

    public void Reset()
    {
        Value = 0;
        countedKeys.Clear();
        hasPrevious = false;
    }

    public void Poll(ISnesMemory memory)
    {
        if (!memory.IsAttached)
        {
            hasPrevious = false;
            return;
        }

        if (!memory.ReadWramByte(MoonCounterOffset, out byte moon)
            || !memory.ReadWramByte(LevelNumOffset, out byte level)
            || !memory.ReadWramByte(RoomNumOffset, out byte room))
        {
            hasPrevious = false;
            return;
        }

        if (hasPrevious && moon > previousMoon)
        {
            int key = DedupePerRoom ? ((level << 8) | room) : level;
            if (countedKeys.Add(key))
            {
                Value++;
            }
        }
        previousMoon = moon;
        hasPrevious = true;
    }

    public void SaveState(XmlDocument doc, XmlElement parent)
    {
        SettingsHelper.CreateSetting(doc, parent, "Moons", Value);
        SettingsHelper.CreateSetting(doc, parent, "DedupePerRoom", DedupePerRoom);
    }

    public void LoadState(XmlElement parent)
    {
        Value = SettingsHelper.ParseInt(parent["Moons"], 0);
        DedupePerRoom = SettingsHelper.ParseBool(parent["DedupePerRoom"], false);
        countedKeys.Clear();
        hasPrevious = false;
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~MoonCounterTests"
```

Expected: PASS — all 7 tests pass.

- [ ] **Step 5: Commit**

```bash
git add test/LiveSplit.Tests/Components/SmwCounters/MoonCounterTests.cs \
        components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/Counters/MoonCounter.cs
git commit -m "Implement MoonCounter with per-level/per-room dedupe tests"
```

---

## Task 8: SmwCountersComponentSettings (data model + XML round-trip)

This task adds the settings as a plain class with XML round-tripping, no UI yet. UI comes in the next task.

**Files:**
- Create: `test/LiveSplit.Tests/Components/SmwCounters/SmwCountersSettingsTests.cs`
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentSettings.cs`

- [ ] **Step 1: Write failing tests**

Path: `test/LiveSplit.Tests/Components/SmwCounters/SmwCountersSettingsTests.cs`

```csharp
using System.Xml;

using LiveSplit.UI.Components;

using Xunit;

namespace LiveSplit.Tests.Components.SmwCounters;

public class SmwCountersSettingsTests
{
    [Fact]
    public void DefaultsHaveNoCountersEnabled()
    {
        var s = new SmwCountersComponentSettings(allowGamepads: false);

        Assert.False(s.IsEnabled("deaths"));
        Assert.False(s.IsEnabled("moons"));
    }

    [Fact]
    public void EnabledSetRoundTripsThroughXml()
    {
        var a = new SmwCountersComponentSettings(allowGamepads: false);
        a.SetEnabled("deaths", true);
        a.SetEnabled("moons", true);

        XmlNode node = a.GetSettings(new XmlDocument { PreserveWhitespace = false });

        var b = new SmwCountersComponentSettings(allowGamepads: false);
        b.SetSettings(node);

        Assert.True(b.IsEnabled("deaths"));
        Assert.True(b.IsEnabled("moons"));
    }

    [Fact]
    public void LabelOverridesRoundTrip()
    {
        var a = new SmwCountersComponentSettings(allowGamepads: false);
        a.SetEnabled("deaths", true);
        a.SetLabelOverride("deaths", "D");

        XmlNode node = a.GetSettings(new XmlDocument());

        var b = new SmwCountersComponentSettings(allowGamepads: false);
        b.SetSettings(node);

        Assert.Equal("D", b.GetLabelOverride("deaths"));
    }

    [Fact]
    public void MissingLabelOverrideReturnsNull()
    {
        var s = new SmwCountersComponentSettings(allowGamepads: false);

        Assert.Null(s.GetLabelOverride("moons"));
    }

    [Fact]
    public void TolereratesMissingSectionsOnLoad()
    {
        // Hand-rolled minimal XML with neither EnabledCounters nor CounterLabels.
        var doc = new XmlDocument();
        XmlElement root = doc.CreateElement("Settings");
        doc.AppendChild(root);
        XmlElement version = doc.CreateElement("Version");
        version.InnerText = "1";
        root.AppendChild(version);

        var s = new SmwCountersComponentSettings(allowGamepads: false);
        s.SetSettings(root); // must not throw

        Assert.False(s.IsEnabled("deaths"));
        Assert.Null(s.GetLabelOverride("deaths"));
    }
}
```

- [ ] **Step 2: Run tests to confirm they fail**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~SmwCountersSettingsTests"
```

Expected: FAIL — `SmwCountersComponentSettings` is not defined.

- [ ] **Step 3: Implement SmwCountersComponentSettings (data model only — UI methods stubbed)**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentSettings.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.Options;

namespace LiveSplit.UI.Components;

public class SmwCountersComponentSettings : UserControl
{
    public CompositeHook Hook { get; }

    private readonly HashSet<string> enabled = new();
    private readonly Dictionary<string, string> labels = new();

    public KeyOrButton ResetKey { get; set; }

    // Component sets this so the settings panel can show live attach state.
    public Func<string> StatusProvider { get; set; }

    public SmwCountersComponentSettings(bool allowGamepads)
    {
        Hook = new CompositeHook(allowGamepads);
        ResetKey = new KeyOrButton(Keys.F2);
        // UI is built in a later task — keep the control empty for now so tests can run.
        Size = new Size(420, 240);
    }

    public bool IsEnabled(string counterId) => enabled.Contains(counterId);

    public void SetEnabled(string counterId, bool value)
    {
        if (value) { enabled.Add(counterId); }
        else { enabled.Remove(counterId); }
    }

    public IEnumerable<string> EnabledIds => enabled;

    public string GetLabelOverride(string counterId) =>
        labels.TryGetValue(counterId, out string s) ? s : null;

    public void SetLabelOverride(string counterId, string label)
    {
        if (string.IsNullOrEmpty(label)) { labels.Remove(counterId); }
        else { labels[counterId] = label; }
    }

    public XmlNode GetSettings(XmlDocument document)
    {
        XmlElement parent = document.CreateElement("Settings");
        CreateSettingsNode(document, parent);
        return parent;
    }

    public void SetSettings(XmlNode node)
    {
        var e = (XmlElement)node;

        XmlElement rst = e["ResetKey"];
        ResetKey = rst != null && !string.IsNullOrEmpty(rst.InnerText) ? new KeyOrButton(rst.InnerText) : null;

        enabled.Clear();
        XmlElement enabledNode = e["EnabledCounters"];
        if (enabledNode != null)
        {
            foreach (XmlElement c in enabledNode.GetElementsByTagName("Counter"))
            {
                if (!string.IsNullOrEmpty(c.InnerText)) { enabled.Add(c.InnerText); }
            }
        }

        labels.Clear();
        XmlElement labelsNode = e["CounterLabels"];
        if (labelsNode != null)
        {
            foreach (XmlElement l in labelsNode.GetElementsByTagName("Label"))
            {
                string id = l.GetAttribute("id");
                if (!string.IsNullOrEmpty(id))
                {
                    labels[id] = l.InnerText ?? "";
                }
            }
        }
    }

    public int GetSettingsHashCode() => CreateSettingsNode(null, null);

    private int CreateSettingsNode(XmlDocument document, XmlElement parent)
    {
        int hash = SettingsHelper.CreateSetting(document, parent, "Version", "1");
        hash ^= SettingsHelper.CreateSetting(document, parent, "ResetKey", ResetKey);

        if (document != null && parent != null)
        {
            XmlElement enabledNode = document.CreateElement("EnabledCounters");
            foreach (string id in enabled)
            {
                XmlElement c = document.CreateElement("Counter");
                c.InnerText = id;
                enabledNode.AppendChild(c);
            }
            parent.AppendChild(enabledNode);

            XmlElement labelsNode = document.CreateElement("CounterLabels");
            foreach (KeyValuePair<string, string> kv in labels)
            {
                XmlElement l = document.CreateElement("Label");
                l.SetAttribute("id", kv.Key);
                l.InnerText = kv.Value;
                labelsNode.AppendChild(l);
            }
            parent.AppendChild(labelsNode);
        }

        foreach (string id in enabled) { hash ^= id.GetHashCode(); }
        foreach (KeyValuePair<string, string> kv in labels)
        {
            hash ^= kv.Key.GetHashCode() ^ (kv.Value ?? "").GetHashCode();
        }
        return hash;
    }
}
```

- [ ] **Step 4: Run tests to confirm they pass**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~SmwCountersSettingsTests"
```

Expected: PASS — all 5 tests pass.

- [ ] **Step 5: Commit**

```bash
git add test/LiveSplit.Tests/Components/SmwCounters/SmwCountersSettingsTests.cs \
        components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentSettings.cs
git commit -m "Add SmwCountersComponentSettings data model with XML round-trip"
```

---

## Task 9: Settings UI

Add the WinForms UI to the settings `UserControl`. The data-model API already added in Task 8 stays; this task only fills in `BuildUi`, the hotkey-capture helper, and the per-counter row widgets.

This task does not have new unit tests (WinForms layout is exercised via manual smoke test in Task 13).

**Files:**
- Modify: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentSettings.cs`

- [ ] **Step 1: Extend the settings UserControl with counter rows and shared widgets**

Insert the following private fields and methods into the existing `SmwCountersComponentSettings` class (between the constructor and `IsEnabled`):

```csharp
    private readonly List<CounterRow> rows = new();
    private TextBox txtReset;
    private Label lblStatus;
    private Timer statusTimer;

    private sealed class CounterRow
    {
        public string Id;
        public CheckBox Enable;
        public TextBox Label;
        public Button ResetValue;
        public Action OnResetValue; // set by the component
        public Control CounterSpecific; // optional extra control (e.g. moon dedupe radio)
    }

    // Component calls this once at construction with the list of known counters.
    public void BuildUi(IReadOnlyList<(string Id, string DefaultLabel, string DefaultGlyph, Control Extras, Action ResetValue)> counters)
    {
        Controls.Clear();
        rows.Clear();

        int y = 10;

        foreach ((string id, string defaultLabel, string defaultGlyph, Control extras, Action resetValue) in counters)
        {
            var row = new CounterRow { Id = id, OnResetValue = resetValue };

            row.Enable = new CheckBox
            {
                Text = defaultLabel,
                Location = new Point(10, y),
                AutoSize = true,
                Checked = IsEnabled(id),
            };
            row.Enable.CheckedChanged += (_, __) => SetEnabled(id, row.Enable.Checked);
            Controls.Add(row.Enable);

            row.Label = new TextBox
            {
                Text = GetLabelOverride(id) ?? "",
                Location = new Point(160, y - 2),
                Width = 100,
            };
            row.Label.TextChanged += (_, __) => SetLabelOverride(id, row.Label.Text);
            Controls.Add(new Label
            {
                Text = $"Label (blank = {defaultGlyph}):",
                Location = new Point(270, y + 2),
                AutoSize = true,
                ForeColor = Color.Gray,
            });

            row.ResetValue = new Button
            {
                Text = "Reset value",
                Location = new Point(420, y - 4),
                AutoSize = true,
            };
            row.ResetValue.Click += (_, __) => row.OnResetValue?.Invoke();
            Controls.Add(row.ResetValue);
            Controls.Add(row.Label);

            y += 28;

            if (extras != null)
            {
                extras.Location = new Point(30, y);
                Controls.Add(extras);
                row.CounterSpecific = extras;
                y += extras.Height + 4;
            }

            rows.Add(row);
        }

        Controls.Add(new Label
        {
            Text = "Reset hotkey (global):",
            Location = new Point(10, y + 3),
            AutoSize = true,
        });
        txtReset = new TextBox
        {
            ReadOnly = true,
            Text = FormatKey(ResetKey),
            Location = new Point(160, y),
            Width = 260,
        };
        txtReset.Enter += (_, __) => CaptureKey(txtReset, k => ResetKey = k);
        Controls.Add(txtReset);
        y += 30;

        Controls.Add(new Label
        {
            Text = "Emulator:",
            Location = new Point(10, y + 3),
            AutoSize = true,
            ForeColor = Color.Gray,
        });
        lblStatus = new Label
        {
            Text = "(detecting…)",
            Location = new Point(160, y + 3),
            AutoSize = true,
            ForeColor = Color.Gray,
        };
        Controls.Add(lblStatus);
        y += 25;

        Controls.Add(new Label
        {
            Text = "Supports snes9x, snes9x-x64, snes9x-rr, bsnes, higan, BizHawk,\n" +
                   "and RetroArch (snes9x_libretro / bsnes_libretro / snes9x2010_libretro).",
            Location = new Point(10, y),
            AutoSize = true,
            ForeColor = Color.Gray,
        });

        Size = new Size(560, y + 60);

        statusTimer?.Dispose();
        statusTimer = new Timer { Interval = 500 };
        statusTimer.Tick += (_, __) =>
        {
            if (StatusProvider != null && lblStatus != null) { lblStatus.Text = StatusProvider(); }
        };
        statusTimer.Enabled = true;

        RegisterHotKeys();
    }

    // Re-syncs visible row widgets from the data model after SetSettings is called.
    public void RefreshFromModel()
    {
        foreach (CounterRow row in rows)
        {
            row.Enable.Checked = IsEnabled(row.Id);
            row.Label.Text = GetLabelOverride(row.Id) ?? "";
        }
        if (txtReset != null) { txtReset.Text = FormatKey(ResetKey); }
        RegisterHotKeys();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing) { statusTimer?.Dispose(); }
        base.Dispose(disposing);
    }

    private void CaptureKey(TextBox box, Action<KeyOrButton> setter)
    {
        string previous = box.Text;
        box.Text = "Set Hotkey...";

        KeyEventHandler keyDown = null;
        EventHandler leave = null;
        EventHandlerT<GamepadButton> gamepad = null;

        void unhook()
        {
            box.KeyDown -= keyDown;
            box.Leave -= leave;
            Hook.AnyGamepadButtonPressed -= gamepad;
        }

        keyDown = (s, e) =>
        {
            e.SuppressKeyPress = true;
            if (e.KeyCode is Keys.ControlKey or Keys.ShiftKey or Keys.Menu) { return; }

            var k = e.KeyCode == Keys.Escape ? null : new KeyOrButton(e.KeyCode | e.Modifiers);
            setter(k);
            unhook();
            box.Text = FormatKey(k);
            ActiveControl = null;
            RegisterHotKeys();
        };

        leave = (_, __) =>
        {
            unhook();
            if (box.Text == "Set Hotkey...") { box.Text = previous; }
        };

        gamepad = (_, btn) =>
        {
            var k = new KeyOrButton(btn);
            setter(k);
            unhook();
            void apply()
            {
                box.Text = FormatKey(k);
                ActiveControl = null;
                RegisterHotKeys();
            }
            if (InvokeRequired) { Invoke(apply); } else { apply(); }
        };

        box.KeyDown += keyDown;
        box.Leave += leave;
        Hook.AnyGamepadButtonPressed += gamepad;
    }

    private void RegisterHotKeys()
    {
        try
        {
            Hook.UnregisterAllHotkeys();
            if (ResetKey != null) { Hook.RegisterHotKey(ResetKey); }
        }
        catch (Exception ex)
        {
            Log.Error(ex);
        }
    }

    private static string FormatKey(KeyOrButton key)
    {
        if (key == null) { return "None"; }
        string s = key.ToString();
        if (key.IsButton)
        {
            int i = s.LastIndexOf(' ');
            if (i != -1) { s = s[..i]; }
        }
        return s;
    }
```

- [ ] **Step 2: Update SetSettings to refresh the UI from the model**

At the bottom of the existing `SetSettings(XmlNode node)` method, append:
```csharp
        RefreshFromModel();
```

- [ ] **Step 3: Build**

```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds.

- [ ] **Step 4: Run all SMW tests to make sure the data-model tests still pass**

```powershell
dotnet test test\LiveSplit.Tests\LiveSplit.Tests.csproj --filter "FullyQualifiedName~Components.SmwCounters"
```

Expected: PASS — all DeathCounter / MoonCounter / Settings tests pass.

- [ ] **Step 5: Commit**

```bash
git add components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentSettings.cs
git commit -m "Build settings UI for SmwCounters"
```

---

## Task 10: SmwCountersComponent

The visual `IComponent` that composes counters, owns the poll loop, draws the cells, and serializes per-counter state.

**Files:**
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponent.cs`

- [ ] **Step 1: Create the component**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponent.cs`

```csharp
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Xml;

using LiveSplit.Model;
using LiveSplit.Model.Input;
using LiveSplit.SmwCounters.Counters;
using LiveSplit.SmwCounters.Snes;

namespace LiveSplit.UI.Components;

public class SmwCountersComponent : IComponent
{
    private const float CellGap = 14f;

    private readonly LiveSplitState state;
    private readonly Timer pollTimer;
    private readonly SnesEmu emu = new();

    // All known counters, registered at construction. The Settings hold the
    // user's enabled subset and label overrides.
    private readonly IReadOnlyList<ISmwCounter> counters;

    private readonly Dictionary<string, SimpleLabel> labelCells = new();
    private readonly Dictionary<string, SimpleLabel> valueCells = new();
    private readonly SimpleLabel statusCell = new();
    private readonly GraphicsCache cache = new();

    public SmwCountersComponentSettings Settings { get; }

    public string ComponentName => "SMW Counters";

    public float VerticalHeight { get; private set; } = 10f;
    public float MinimumHeight { get; private set; }
    public float HorizontalWidth { get; private set; }
    public float MinimumWidth => 80f;

    public float PaddingTop { get; private set; }
    public float PaddingBottom { get; private set; }
    public float PaddingLeft => 7f;
    public float PaddingRight => 7f;

    public IDictionary<string, Action> ContextMenuControls => null;

    public SmwCountersComponent(LiveSplitState state)
    {
        this.state = state;

        // Build the registry of known counters.
        var moon = new MoonCounter();
        counters = new ISmwCounter[]
        {
            new DeathCounter(),
            moon,
        };

        foreach (ISmwCounter c in counters)
        {
            labelCells[c.Id] = new SimpleLabel();
            valueCells[c.Id] = new SimpleLabel();
        }

        bool allowGamepads = state.Settings.HotkeyProfiles.First().Value.AllowGamepadsAsHotkeys;
        Settings = new SmwCountersComponentSettings(allowGamepads);
        Settings.Hook.KeyOrButtonPressed += Hook_KeyOrButtonPressed;
        Settings.StatusProvider = () => emu.Describe();

        // Wire up per-counter rows. Counter-specific extras live here so the
        // settings UserControl doesn't know about individual counter types.
        var rows = new List<(string Id, string DefaultLabel, string DefaultGlyph, Control Extras, Action ResetValue)>();
        foreach (ISmwCounter c in counters)
        {
            Control extras = BuildExtras(c);
            rows.Add((c.Id, c.DefaultLabel, c.DefaultGlyph, extras, () => c.Reset()));
        }
        Settings.BuildUi(rows);

        pollTimer = new Timer { Interval = 15 };
        pollTimer.Tick += (_, __) => Poll();
        pollTimer.Enabled = true;
    }

    private Control BuildExtras(ISmwCounter counter)
    {
        if (counter is MoonCounter moon)
        {
            var panel = new Panel { Width = 400, Height = 24, Padding = new Padding(0) };
            var rdoLevel = new RadioButton
            {
                Text = "Per level",
                AutoSize = true,
                Checked = !moon.DedupePerRoom,
                Location = new Point(0, 4),
            };
            var rdoRoom = new RadioButton
            {
                Text = "Per room (level + sublevel)",
                AutoSize = true,
                Checked = moon.DedupePerRoom,
                Location = new Point(90, 4),
            };
            rdoLevel.CheckedChanged += (_, __) => { if (rdoLevel.Checked) { moon.DedupePerRoom = false; } };
            rdoRoom.CheckedChanged  += (_, __) => { if (rdoRoom.Checked)  { moon.DedupePerRoom = true; } };
            panel.Controls.Add(rdoLevel);
            panel.Controls.Add(rdoRoom);
            return panel;
        }
        return null;
    }

    private void Hook_KeyOrButtonPressed(object sender, KeyOrButton e)
    {
        if (e == Settings.ResetKey)
        {
            foreach (ISmwCounter c in counters)
            {
                if (Settings.IsEnabled(c.Id)) { c.Reset(); }
            }
        }
    }

    private void Poll()
    {
        if (!emu.TryAttach()) { return; }
        foreach (ISmwCounter c in counters)
        {
            if (Settings.IsEnabled(c.Id)) { c.Poll(emu); }
        }
    }

    private string LabelTextFor(ISmwCounter c)
    {
        string overrideText = Settings.GetLabelOverride(c.Id);
        return string.IsNullOrEmpty(overrideText) ? c.DefaultGlyph : overrideText;
    }

    public void Update(IInvalidator invalidator, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        try { Settings.Hook?.Poll(); } catch { }

        cache.Restart();
        foreach (ISmwCounter c in counters)
        {
            if (!Settings.IsEnabled(c.Id)) { continue; }
            string label = LabelTextFor(c);
            string value = c.Value.ToString();
            labelCells[c.Id].Text = label;
            valueCells[c.Id].Text = value;
            cache[c.Id + ".label"] = label;
            cache[c.Id + ".value"] = value;
        }
        string status = emu.IsAttached ? "" : "◌";
        statusCell.Text = status;
        cache["status"] = status;

        if (invalidator != null && cache.HasChanged)
        {
            invalidator.Invalidate(0, 0, width, height);
        }
    }

    private void DrawGeneral(Graphics g, LiveSplitState state, float width, float height, LayoutMode mode)
    {
        Font font = state.LayoutSettings.TextFont;
        Color textColor = state.LayoutSettings.TextColor;

        float textHeight = g.MeasureString("A", font).Height;
        VerticalHeight = 1.2f * textHeight;
        PaddingTop = Math.Max(0, (VerticalHeight - (0.75f * textHeight)) / 2f);
        PaddingBottom = PaddingTop;

        // Measure each enabled counter's cell width: label + " " + value.
        var enabled = counters.Where(c => Settings.IsEnabled(c.Id)).ToList();
        float totalWidth = 0f;
        var cellWidths = new Dictionary<string, (float labelW, float valueW)>();
        foreach (ISmwCounter c in enabled)
        {
            float labelW = g.MeasureString(LabelTextFor(c), font).Width;
            float valueW = g.MeasureString(c.Value.ToString("0"), font).Width;
            cellWidths[c.Id] = (labelW, valueW);
            if (totalWidth > 0) { totalWidth += CellGap; }
            totalWidth += labelW + 4 + valueW;
        }

        float statusW = string.IsNullOrEmpty(statusCell.Text) ? 0f : g.MeasureString(" ◌", font).Width;
        HorizontalWidth = totalWidth + statusW + 15;

        float x = 5f;
        foreach (ISmwCounter c in enabled)
        {
            (float labelW, float valueW) = cellWidths[c.Id];
            ConfigureLabel(labelCells[c.Id], font, textColor, StringAlignment.Near, x, labelW, height);
            x += labelW + 4;
            ConfigureLabel(valueCells[c.Id], font, textColor, StringAlignment.Near, x, valueW, height);
            x += valueW + CellGap;
            labelCells[c.Id].Draw(g);
            valueCells[c.Id].Draw(g);
        }

        if (!string.IsNullOrEmpty(statusCell.Text))
        {
            ConfigureLabel(statusCell, font, textColor, StringAlignment.Far, 5, width - 10, height);
            statusCell.Draw(g);
        }
    }

    private void ConfigureLabel(SimpleLabel label, Font font, Color color, StringAlignment hAlign, float x, float width, float height)
    {
        label.HorizontalAlignment = hAlign;
        label.VerticalAlignment = StringAlignment.Center;
        label.X = x;
        label.Y = 0;
        label.Width = width;
        label.Height = height;
        label.Font = font;
        label.Brush = new SolidBrush(color);
        label.HasShadow = state.LayoutSettings.DropShadows;
        label.ShadowColor = state.LayoutSettings.ShadowsColor;
        label.OutlineColor = state.LayoutSettings.TextOutlineColor;
    }

    public void DrawHorizontal(Graphics g, LiveSplitState state, float height, Region clipRegion)
        => DrawGeneral(g, state, HorizontalWidth, height, LayoutMode.Horizontal);

    public void DrawVertical(Graphics g, LiveSplitState state, float width, Region clipRegion)
        => DrawGeneral(g, state, width, VerticalHeight, LayoutMode.Vertical);

    public Control GetSettingsControl(LayoutMode mode) => Settings;

    public XmlNode GetSettings(XmlDocument document)
    {
        var node = (XmlElement)Settings.GetSettings(document);

        XmlElement stateNode = document.CreateElement("CounterState");
        foreach (ISmwCounter c in counters)
        {
            XmlElement el = document.CreateElement(c.Id);
            c.SaveState(document, el);
            stateNode.AppendChild(el);
        }
        node.AppendChild(stateNode);
        return node;
    }

    public void SetSettings(XmlNode settings)
    {
        Settings.SetSettings(settings);

        XmlElement stateNode = ((XmlElement)settings)["CounterState"];
        if (stateNode != null)
        {
            foreach (ISmwCounter c in counters)
            {
                XmlElement el = stateNode[c.Id];
                if (el != null) { c.LoadState(el); }
            }
        }
    }

    public int GetSettingsHashCode()
    {
        int hash = Settings.GetSettingsHashCode();
        foreach (ISmwCounter c in counters) { hash ^= c.Value.GetHashCode(); }
        return hash;
    }

    public void Dispose()
    {
        pollTimer?.Dispose();
        Settings.Hook.KeyOrButtonPressed -= Hook_KeyOrButtonPressed;
        Settings.Hook.UnregisterAllHotkeys();
    }
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponent.cs
git commit -m "Add SmwCountersComponent that composes counters into a single row"
```

---

## Task 11: Component factory

**Files:**
- Create: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentFactory.cs`

- [ ] **Step 1: Create the factory**

Path: `components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentFactory.cs`

```csharp
using System;

using LiveSplit.Model;
using LiveSplit.UI.Components;

[assembly: ComponentFactory(typeof(SmwCountersComponentFactory))]

namespace LiveSplit.UI.Components;

public class SmwCountersComponentFactory : IComponentFactory
{
    public string ComponentName => "SMW Counters";

    public string Description => "Watches SNES WRAM in an emulator process and counts SMW deaths, moons, and more — pick which to show.";

    public ComponentCategory Category => ComponentCategory.Other;

    public IComponent Create(LiveSplitState state) => new SmwCountersComponent(state);

    public string UpdateName => ComponentName;

    public string XMLURL => string.Empty;

    public string UpdateURL => string.Empty;

    public Version Version => Version.Parse("0.1.0");
}
```

- [ ] **Step 2: Build**

```powershell
dotnet build components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj
```

Expected: build succeeds.

- [ ] **Step 3: Commit**

```bash
git add components/LiveSplit.SmwCounters/src/LiveSplit.SmwCounters/UI/Components/SmwCountersComponentFactory.cs
git commit -m "Register SmwCountersComponentFactory"
```

---

## Task 12: Wire into LiveSplit.sln, remove old SMW projects

The `.sln` lists the new project once and the old two are removed. Each old project has entries in four places (Project line, ProjectDependencies of `LiveSplit`, GlobalSection ProjectConfigurationPlatforms, NestedProjects). Walk through each.

The new project's GUID will be `{B7E7C0DE-5555-4222-9333-AABBCCDDEE05}` — continues the convention used for `PressCounter`/`ScrollingGraph`/etc.

**Files:**
- Modify: `LiveSplit.sln`

- [ ] **Step 1: Replace the SmwDeathCounter Project line (around line 60)**

Old:
```
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "LiveSplit.SmwDeathCounter", "components\LiveSplit.SmwDeathCounter\src\LiveSplit.SmwDeathCounter\LiveSplit.SmwDeathCounter.csproj", "{B7E7C0DE-2222-4222-9333-AABBCCDDEE02}"
EndProject
```

New:
```
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "LiveSplit.SmwCounters", "components\LiveSplit.SmwCounters\src\LiveSplit.SmwCounters\LiveSplit.SmwCounters.csproj", "{B7E7C0DE-5555-4222-9333-AABBCCDDEE05}"
EndProject
```

- [ ] **Step 2: Delete the SmwMoonCounter Project line (around line 64)**

Old:
```
Project("{9A19103F-16F7-4668-BE54-9A1E7A4F7556}") = "LiveSplit.SmwMoonCounter", "components\LiveSplit.SmwMoonCounter\src\LiveSplit.SmwMoonCounter\LiveSplit.SmwMoonCounter.csproj", "{B7E7C0DE-4444-4222-9333-AABBCCDDEE04}"
EndProject
```

Delete both lines (the `Project` line and its `EndProject`).

- [ ] **Step 3: In the LiveSplit project's ProjectDependencies block (around lines 102–105), replace -2222- and delete -4444-**

Replace:
```
		{B7E7C0DE-2222-4222-9333-AABBCCDDEE02} = {B7E7C0DE-2222-4222-9333-AABBCCDDEE02}
```
with:
```
		{B7E7C0DE-5555-4222-9333-AABBCCDDEE05} = {B7E7C0DE-5555-4222-9333-AABBCCDDEE05}
```

And delete the line:
```
		{B7E7C0DE-4444-4222-9333-AABBCCDDEE04} = {B7E7C0DE-4444-4222-9333-AABBCCDDEE04}
```

- [ ] **Step 4: In GlobalSection(ProjectConfigurationPlatforms), replace -2222- block (4 lines) and delete -4444- block (4 lines)**

Replace this block (around lines 191–194):
```
		{B7E7C0DE-2222-4222-9333-AABBCCDDEE02}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B7E7C0DE-2222-4222-9333-AABBCCDDEE02}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B7E7C0DE-2222-4222-9333-AABBCCDDEE02}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B7E7C0DE-2222-4222-9333-AABBCCDDEE02}.Release|Any CPU.Build.0 = Release|Any CPU
```
with:
```
		{B7E7C0DE-5555-4222-9333-AABBCCDDEE05}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B7E7C0DE-5555-4222-9333-AABBCCDDEE05}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B7E7C0DE-5555-4222-9333-AABBCCDDEE05}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B7E7C0DE-5555-4222-9333-AABBCCDDEE05}.Release|Any CPU.Build.0 = Release|Any CPU
```

Delete this block (around lines 199–202):
```
		{B7E7C0DE-4444-4222-9333-AABBCCDDEE04}.Debug|Any CPU.ActiveCfg = Debug|Any CPU
		{B7E7C0DE-4444-4222-9333-AABBCCDDEE04}.Debug|Any CPU.Build.0 = Debug|Any CPU
		{B7E7C0DE-4444-4222-9333-AABBCCDDEE04}.Release|Any CPU.ActiveCfg = Release|Any CPU
		{B7E7C0DE-4444-4222-9333-AABBCCDDEE04}.Release|Any CPU.Build.0 = Release|Any CPU
```

- [ ] **Step 5: In GlobalSection(NestedProjects), replace -2222- and delete -4444-**

Replace (around line 330):
```
		{B7E7C0DE-2222-4222-9333-AABBCCDDEE02} = {EE292569-BD0A-4F22-BD2E-1202B33CA83E}
```
with:
```
		{B7E7C0DE-5555-4222-9333-AABBCCDDEE05} = {EE292569-BD0A-4F22-BD2E-1202B33CA83E}
```

Delete (around line 332):
```
		{B7E7C0DE-4444-4222-9333-AABBCCDDEE04} = {EE292569-BD0A-4F22-BD2E-1202B33CA83E}
```

- [ ] **Step 6: Verify there are no remaining references to the deleted GUIDs**

Run from the repo root:
```powershell
Select-String -Path LiveSplit.sln -Pattern "B7E7C0DE-2222|B7E7C0DE-4444|SmwDeathCounter|SmwMoonCounter"
```

Expected: no output (no matches).

- [ ] **Step 7: Build the whole solution**

```powershell
dotnet build LiveSplit.sln
```

Expected: build succeeds. The new `LiveSplit.SmwCounters.dll` is dropped into `bin\Debug\Components\` (by virtue of `EnableDynamicLoading=true` and the `LiveSplit` app's reference of components by GUID).

If `LiveSplit.csproj` had hard-coded `Reference Include`s for the old DLLs they'd need fixing here, but the component model uses runtime reflection only — verify there's no compile-time dependency by grepping:
```powershell
Select-String -Path src\LiveSplit\LiveSplit.csproj -Pattern "SmwDeath|SmwMoon"
```
Expected: no output.

- [ ] **Step 8: Commit**

```bash
git add LiveSplit.sln
git commit -m "Replace SmwDeathCounter and SmwMoonCounter with SmwCounters in solution"
```

---

## Task 13: Delete the old component folders

**Files:**
- Delete: `components/LiveSplit.SmwDeathCounter/` (tracked)
- Delete: `components/LiveSplit.SmwMoonCounter/` (untracked)

- [ ] **Step 1: Remove the tracked SmwDeathCounter folder via git**

```powershell
git rm -r components\LiveSplit.SmwDeathCounter
```

- [ ] **Step 2: Remove the untracked SmwMoonCounter folder**

```powershell
Remove-Item -Recurse -Force components\LiveSplit.SmwMoonCounter
```

- [ ] **Step 3: Confirm the working tree is clean of the old plugins**

```powershell
git status
```

Expected: shows the deletion of all `SmwDeathCounter` files staged for commit, and no `SmwMoonCounter` directory remains.

- [ ] **Step 4: Build the whole solution one more time to confirm nothing broke**

```powershell
dotnet build LiveSplit.sln
```

Expected: build succeeds.

- [ ] **Step 5: Run the full test suite**

```powershell
dotnet test LiveSplit.sln
```

Expected: all tests pass — including the 6 DeathCounter, 7 MoonCounter, and 5 settings tests added in this plan, plus all pre-existing tests.

- [ ] **Step 6: Commit**

```bash
git commit -m "Remove old SmwDeathCounter and SmwMoonCounter component folders"
```

---

## Task 14: Manual smoke test

LiveSplit components have logic (emulator detection, hotkeys, painting) that the unit tests can't exercise. Verify by hand.

- [ ] **Step 1: Confirm Debug build is fresh**

```powershell
dotnet build LiveSplit.sln
```

Expected: succeeds. `bin\Debug\Components\LiveSplit.SmwCounters.dll` exists.

```powershell
Test-Path bin\Debug\Components\LiveSplit.SmwCounters.dll
```
Expected: `True`.

```powershell
Test-Path bin\Debug\Components\LiveSplit.SmwDeathCounter.dll
Test-Path bin\Debug\Components\LiveSplit.SmwMoonCounter.dll
```
Expected: both `False`.

- [ ] **Step 2: Launch LiveSplit**

```powershell
.\bin\Debug\LiveSplit.exe
```

- [ ] **Step 3: Add the component to the layout**

In LiveSplit: right-click → Edit Layout → Add → Other → **SMW Counters**. Confirm it appears in the layout editor with the new settings panel.

- [ ] **Step 4: Verify settings UI**

In the SMW Counters settings tab:
- Enable "Deaths" — confirm a 💀 cell with `0` appears next to the ◌ (detached) status glyph.
- Enable "Moons" — confirm a 🌙 `0` cell appears immediately to the right of the Deaths cell on the same row.
- Change the Deaths label to "D" — cell updates live to `D 0`.
- Switch Moons dedupe to "Per room" — radio button toggles, no crash.

- [ ] **Step 5: Save and reload the layout**

Save the layout to a `.lsl`, close LiveSplit, reopen with that layout (`LiveSplit.exe -l <path>.lsl`). Verify:
- Both counters re-appear with the labels you set.
- The values are persisted (e.g. if a `Deaths` value was previously >0, it survived the reload).
- The Moons "Per room" choice is preserved.

- [ ] **Step 6: (Optional, requires emulator) Verify attach + counting**

Launch any supported emulator with a SMW ROM (e.g. snes9x). In LiveSplit, observe:
- The ◌ status glyph disappears once attached.
- Triggering a death in-game increments the Deaths counter by 1.
- Collecting a 3-up moon increments the Moons counter by 1.
- Pressing F2 (default reset hotkey) zeroes both values.

If no emulator is available, leave this step unchecked and call it out in the PR description.

- [ ] **Step 7: Record results**

If all the above succeeded, this task is complete. If anything failed, file a follow-up task with the exact failure mode rather than patching ad-hoc.

---

## Final notes

- The plan does **not** bump anything in `update.xml` or run a release — the spec calls out releases are out of scope for this work.
- The plan does **not** add icon assets — emoji glyphs cover v1, per the spec.
- The plan does **not** touch `PressCounter` — unrelated to SMW unification.
