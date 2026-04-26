# SpaceMouse R.E.P.O. Mod Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a Thunderstore-distributable BepInEx plugin that lets a player use a 3Dconnexion SpaceMouse Wireless to rotate held objects (full 6DOF) and apply a small clamped local offset, in R.E.P.O., client-side only.

**Architecture:** Two-project .NET solution. `SpaceMouseRepo.Core` (netstandard2.0) holds all engine-agnostic logic — HID report parsing and held-object math — and is unit-tested via `SpaceMouseRepo.Tests` (net8.0) that runs natively on macOS via `dotnet test`. `SpaceMouseRepo.Plugin` (net472) is the BepInEx 5 plugin that does HID device I/O, BepInEx config binding, and Harmony patching of R.E.P.O.'s `PhysGrabber`. `Plugin` references `Core` for parsing and math, so anything not touching Unity/BepInEx APIs is testable on macOS.

**Tech Stack:** C# 10, .NET Framework 4.7.2 (plugin) + netstandard2.0 (core) + .NET 8 (tests), BepInEx 5, HarmonyX, HidLibrary, System.Numerics for math, xUnit for tests, GitHub Actions Windows runners for build.

---

## Pre-flight assumptions

- macOS development host with `dotnet` SDK 8+ installed. Verify via `dotnet --list-sdks` (must show an 8.x line). Install via `brew install --cask dotnet-sdk` if missing.
- Tasks 1–10 are completable on macOS without the game running. Task 11 requires a Windows machine, the SpaceMouse, and an installed copy of R.E.P.O.
- The user owns a SpaceMouse Wireless (vendor 0x256F). HID product IDs 0xC62E, 0xC62F, 0xC652 are listed; if the device reports a different ID, Task 11 includes a diagnostic to find it.
- No 3Dconnexion proprietary SDK is required at runtime — all I/O is raw HID via the `HidLibrary` NuGet.
- We will NOT decompile R.E.P.O. up-front. Per spec sign-off, we ship a v0.0.1 with our best-guess `PhysGrabber.Update` postfix and iterate on logged failures from real-world testing.

---

## Task 1: Project skeleton & .gitignore

End state: `dotnet build src/SpaceMouseRepo.sln` succeeds with zero source files; `dotnet test src/SpaceMouseRepo.sln` reports 0 passed/0 failed.

**Files:**
- Create: `.gitignore`
- Create: `src/SpaceMouseRepo.sln`
- Create: `src/SpaceMouseRepo.Core/SpaceMouseRepo.Core.csproj`
- Create: `src/SpaceMouseRepo.Plugin/SpaceMouseRepo.Plugin.csproj`
- Create: `src/SpaceMouseRepo.Tests/SpaceMouseRepo.Tests.csproj`
- Create: `src/SpaceMouseRepo.Tests/_Probe.cs`

- [ ] **Step 1: Create the .gitignore at repo root**

Contents:
```
bin/
obj/
*.user
*.suo
.vs/
.idea/
.vscode/
*.DotSettings.user

# Build artifacts
*.dll
*.pdb
*.zip

# OS
.DS_Store
Thumbs.db
```

- [ ] **Step 2: Create `src/SpaceMouseRepo.Core/SpaceMouseRepo.Core.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <RootNamespace>SpaceMouseRepo.Core</RootNamespace>
    <AssemblyName>SpaceMouseRepo.Core</AssemblyName>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="System.Numerics.Vectors" Version="4.5.0" />
  </ItemGroup>
</Project>
```

- [ ] **Step 3: Create `src/SpaceMouseRepo.Plugin/SpaceMouseRepo.Plugin.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net472</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <RootNamespace>SpaceMouseRepo</RootNamespace>
    <AssemblyName>SpaceMouseRepo</AssemblyName>
    <CopyLocalLockFileAssemblies>true</CopyLocalLockFileAssemblies>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="BepInEx.Core" Version="5.4.21" />
    <PackageReference Include="BepInEx.PluginInfoProps" Version="2.1.0" />
    <PackageReference Include="HarmonyX" Version="2.10.2" />
    <PackageReference Include="HidLibrary" Version="3.3.40" />
    <PackageReference Include="UnityEngine.Modules" Version="2022.3.21" IncludeAssets="compile" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SpaceMouseRepo.Core\SpaceMouseRepo.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 4: Create `src/SpaceMouseRepo.Tests/SpaceMouseRepo.Tests.csproj`**

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <LangVersion>10</LangVersion>
    <Nullable>enable</Nullable>
    <IsPackable>false</IsPackable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.8.0" />
    <PackageReference Include="xunit" Version="2.6.6" />
    <PackageReference Include="xunit.runner.visualstudio" Version="2.5.6" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="..\SpaceMouseRepo.Core\SpaceMouseRepo.Core.csproj" />
  </ItemGroup>
</Project>
```

- [ ] **Step 5: Create `src/SpaceMouseRepo.Tests/_Probe.cs` (throwaway test to prove the harness wires up)**

```csharp
using Xunit;

namespace SpaceMouseRepo.Tests;

public class Probe
{
    [Fact]
    public void Harness_runs() => Assert.True(true);
}
```

- [ ] **Step 6: Create `src/SpaceMouseRepo.sln`**

Run from `src/`:
```
cd src
dotnet new sln -n SpaceMouseRepo
dotnet sln add SpaceMouseRepo.Core/SpaceMouseRepo.Core.csproj
dotnet sln add SpaceMouseRepo.Plugin/SpaceMouseRepo.Plugin.csproj
dotnet sln add SpaceMouseRepo.Tests/SpaceMouseRepo.Tests.csproj
cd ..
```

- [ ] **Step 7: Verify build**

Run: `dotnet build src/SpaceMouseRepo.sln`
Expected: `Build succeeded.` with 0 errors. (Warnings about UnityEngine.Modules are OK — that NuGet is reference-only.)

- [ ] **Step 8: Verify test harness**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: `Passed!  - Failed: 0, Passed: 1, Skipped: 0` (the `Harness_runs` probe).

- [ ] **Step 9: Delete `_Probe.cs`** — it has served its purpose.

- [ ] **Step 10: Commit**

```bash
git add .gitignore src/
git commit -m "chore: project skeleton with Core/Plugin/Tests"
```

---

## Task 2: `SpaceMouseState` — the immutable input snapshot

The data type that flows from HID parser → math layer → patch. Immutable struct; no engine references; unit-tested for trivial round-tripping.

**Files:**
- Create: `src/SpaceMouseRepo.Core/Input/SpaceMouseState.cs`
- Create: `src/SpaceMouseRepo.Tests/Input/SpaceMouseStateTests.cs`

- [ ] **Step 1: Write the failing test**

Create `src/SpaceMouseRepo.Tests/Input/SpaceMouseStateTests.cs`:

```csharp
using SpaceMouseRepo.Core.Input;
using Xunit;

namespace SpaceMouseRepo.Tests.Input;

public class SpaceMouseStateTests
{
    [Fact]
    public void Empty_is_all_zeros_and_no_buttons()
    {
        var s = SpaceMouseState.Empty;
        Assert.Equal(0f, s.Tx);
        Assert.Equal(0f, s.Ty);
        Assert.Equal(0f, s.Tz);
        Assert.Equal(0f, s.Rx);
        Assert.Equal(0f, s.Ry);
        Assert.Equal(0f, s.Rz);
        Assert.False(s.Button1);
        Assert.False(s.Button2);
    }

    [Fact]
    public void Constructor_round_trips_values()
    {
        var s = new SpaceMouseState(0.1f, 0.2f, 0.3f, 0.4f, 0.5f, 0.6f, true, false);
        Assert.Equal(0.1f, s.Tx);
        Assert.Equal(0.6f, s.Rz);
        Assert.True(s.Button1);
        Assert.False(s.Button2);
    }
}
```

- [ ] **Step 2: Run, confirm failure**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: 2 failed (`SpaceMouseState` does not exist).

- [ ] **Step 3: Write minimal implementation**

Create `src/SpaceMouseRepo.Core/Input/SpaceMouseState.cs`:

```csharp
namespace SpaceMouseRepo.Core.Input;

public readonly struct SpaceMouseState
{
    public static readonly SpaceMouseState Empty = default;

    public float Tx { get; }
    public float Ty { get; }
    public float Tz { get; }
    public float Rx { get; }
    public float Ry { get; }
    public float Rz { get; }
    public bool Button1 { get; }
    public bool Button2 { get; }

    public SpaceMouseState(float tx, float ty, float tz, float rx, float ry, float rz, bool button1, bool button2)
    {
        Tx = tx; Ty = ty; Tz = tz;
        Rx = rx; Ry = ry; Rz = rz;
        Button1 = button1; Button2 = button2;
    }
}
```

- [ ] **Step 4: Run, confirm pass**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: `Passed: 2, Failed: 0`.

- [ ] **Step 5: Commit**

```bash
git add src/SpaceMouseRepo.Core/Input/ src/SpaceMouseRepo.Tests/Input/
git commit -m "feat(core): add SpaceMouseState immutable snapshot"
```

---

## Task 3: `SpaceMouseReportParser` — translation, rotation, button reports + deadzone

Stateful parser fed raw HID report bytes. Outputs `SpaceMouseState`. Reports are parsed by their leading report-ID byte. Translation and rotation arrive in separate reports, so the parser remembers the last-seen translation when a rotation report comes in (and vice versa). Deadzone applied at output time.

**HID report format (3Dconnexion standard, confirmed across SpaceMouse Wireless / Compact / Pro):**

| Report ID | Length | Layout |
|---|---|---|
| 0x01 | 7 bytes | `[0x01, Tx_lo, Tx_hi, Ty_lo, Ty_hi, Tz_lo, Tz_hi]` (int16 little-endian per axis) |
| 0x02 | 7 bytes | `[0x02, Rx_lo, Rx_hi, Ry_lo, Ry_hi, Rz_lo, Rz_hi]` |
| 0x03 | 3 bytes | `[0x03, btn_lo, btn_hi]` (button bitmask, bit 0 = Button1, bit 1 = Button2) |

Raw axis range is approximately `[-350, 350]`. Normalize by dividing by 350 and clamping to `[-1, 1]`.

**Files:**
- Create: `src/SpaceMouseRepo.Core/Input/SpaceMouseReportParser.cs`
- Create: `src/SpaceMouseRepo.Tests/Input/SpaceMouseReportParserTests.cs`

- [ ] **Step 1: Write failing tests**

Create `src/SpaceMouseRepo.Tests/Input/SpaceMouseReportParserTests.cs`:

```csharp
using SpaceMouseRepo.Core.Input;
using Xunit;

namespace SpaceMouseRepo.Tests.Input;

public class SpaceMouseReportParserTests
{
    private static byte[] TranslationReport(short tx, short ty, short tz) =>
        new byte[] { 0x01, (byte)(tx & 0xFF), (byte)((tx >> 8) & 0xFF),
                           (byte)(ty & 0xFF), (byte)((ty >> 8) & 0xFF),
                           (byte)(tz & 0xFF), (byte)((tz >> 8) & 0xFF) };

    private static byte[] RotationReport(short rx, short ry, short rz) =>
        new byte[] { 0x02, (byte)(rx & 0xFF), (byte)((rx >> 8) & 0xFF),
                           (byte)(ry & 0xFF), (byte)((ry >> 8) & 0xFF),
                           (byte)(rz & 0xFF), (byte)((rz >> 8) & 0xFF) };

    private static byte[] ButtonReport(ushort buttons) =>
        new byte[] { 0x03, (byte)(buttons & 0xFF), (byte)((buttons >> 8) & 0xFF) };

    [Fact]
    public void Initial_state_is_empty()
    {
        var p = new SpaceMouseReportParser();
        Assert.Equal(SpaceMouseState.Empty, p.State);
    }

    [Fact]
    public void Translation_report_normalizes_to_minus_one_to_one()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(TranslationReport(350, -350, 175));
        Assert.Equal(1.0f, p.State.Tx, 3);
        Assert.Equal(-1.0f, p.State.Ty, 3);
        Assert.Equal(0.5f, p.State.Tz, 3);
    }

    [Fact]
    public void Translation_report_clamps_overflow_to_one()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(TranslationReport(700, -700, 0));
        Assert.Equal(1.0f, p.State.Tx, 3);
        Assert.Equal(-1.0f, p.State.Ty, 3);
    }

    [Fact]
    public void Rotation_report_does_not_disturb_translation()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(TranslationReport(175, 0, 0));
        p.Feed(RotationReport(350, 0, 0));
        Assert.Equal(0.5f, p.State.Tx, 3);
        Assert.Equal(1.0f, p.State.Rx, 3);
    }

    [Fact]
    public void Button_report_sets_pressed_bits()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(ButtonReport(0b01));
        Assert.True(p.State.Button1);
        Assert.False(p.State.Button2);

        p.Feed(ButtonReport(0b10));
        Assert.False(p.State.Button1);
        Assert.True(p.State.Button2);

        p.Feed(ButtonReport(0b11));
        Assert.True(p.State.Button1);
        Assert.True(p.State.Button2);

        p.Feed(ButtonReport(0));
        Assert.False(p.State.Button1);
        Assert.False(p.State.Button2);
    }

    [Fact]
    public void Deadzone_zeros_axes_below_threshold()
    {
        var p = new SpaceMouseReportParser(translationDeadzone: 0.1f, rotationDeadzone: 0.1f);
        p.Feed(TranslationReport(17, 35, 175));   // 0.0486, 0.1, 0.5
        Assert.Equal(0f, p.State.Tx);             // below 0.1 deadzone
        Assert.Equal(0f, p.State.Ty);             // exactly at deadzone, treat as zero
        Assert.Equal(0.5f, p.State.Tz, 3);        // above deadzone, passes through
    }

    [Fact]
    public void Unknown_report_id_is_ignored_and_state_unchanged()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(TranslationReport(175, 0, 0));
        p.Feed(new byte[] { 0xFF, 0xAA, 0xBB });
        Assert.Equal(0.5f, p.State.Tx, 3);
    }

    [Fact]
    public void Empty_or_too_short_report_is_ignored()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(new byte[0]);
        p.Feed(new byte[] { 0x01, 0x00 });
        Assert.Equal(SpaceMouseState.Empty, p.State);
    }
}
```

- [ ] **Step 2: Run, confirm failures**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: 8 failed (`SpaceMouseReportParser` does not exist).

- [ ] **Step 3: Write the implementation**

Create `src/SpaceMouseRepo.Core/Input/SpaceMouseReportParser.cs`:

```csharp
using System;

namespace SpaceMouseRepo.Core.Input;

public sealed class SpaceMouseReportParser
{
    private const float AxisDivisor = 350f;

    private readonly float _translationDeadzone;
    private readonly float _rotationDeadzone;

    private float _tx, _ty, _tz, _rx, _ry, _rz;
    private bool _b1, _b2;

    public SpaceMouseReportParser(float translationDeadzone = 0f, float rotationDeadzone = 0f)
    {
        _translationDeadzone = translationDeadzone;
        _rotationDeadzone = rotationDeadzone;
    }

    public SpaceMouseState State => new(
        Apply(_tx, _translationDeadzone),
        Apply(_ty, _translationDeadzone),
        Apply(_tz, _translationDeadzone),
        Apply(_rx, _rotationDeadzone),
        Apply(_ry, _rotationDeadzone),
        Apply(_rz, _rotationDeadzone),
        _b1, _b2);

    public void Feed(byte[] report)
    {
        if (report == null || report.Length == 0) return;
        switch (report[0])
        {
            case 0x01 when report.Length >= 7:
                _tx = ReadAxis(report, 1);
                _ty = ReadAxis(report, 3);
                _tz = ReadAxis(report, 5);
                break;
            case 0x02 when report.Length >= 7:
                _rx = ReadAxis(report, 1);
                _ry = ReadAxis(report, 3);
                _rz = ReadAxis(report, 5);
                break;
            case 0x03 when report.Length >= 2:
                ushort mask = (ushort)(report[1] | (report.Length >= 3 ? report[2] << 8 : 0));
                _b1 = (mask & 0x01) != 0;
                _b2 = (mask & 0x02) != 0;
                break;
        }
    }

    private static float ReadAxis(byte[] r, int offset)
    {
        short raw = (short)(r[offset] | (r[offset + 1] << 8));
        float v = raw / AxisDivisor;
        return v switch { > 1f => 1f, < -1f => -1f, _ => v };
    }

    private static float Apply(float v, float deadzone)
        => Math.Abs(v) <= deadzone ? 0f : v;
}
```

- [ ] **Step 4: Run, confirm pass**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: `Passed: 10, Failed: 0` (2 from Task 2 + 8 here).

- [ ] **Step 5: Commit**

```bash
git add src/SpaceMouseRepo.Core/Input/ src/SpaceMouseRepo.Tests/Input/
git commit -m "feat(core): SpaceMouseReportParser with deadzone"
```

---

## Task 4: `HeldObjectController` — accumulator math (rotation, translation, reset, precision)

Pure-math behavior layer. Takes `SpaceMouseState` + dt, accumulates rotation (Quaternion) and clamped offset (Vector3), exposes the latest accumulated values. Uses `System.Numerics.Quaternion` and `System.Numerics.Vector3` so the test project (net8.0 on macOS) can exercise it without Unity. The plugin layer will convert to `UnityEngine.Quaternion` / `UnityEngine.Vector3` at the patch boundary.

**Behavior matrix:**

| Input | Effect |
|---|---|
| `Apply(s, dt)` while held, axis above deadzone | `accRot = Quaternion.Euler(s.Rxyz * rotSensDegPerSec * dt) * accRot`; `accOffset += s.Txyz * transSensMPerSec * dt`; offset clamped to `MaxOffsetM` |
| `Apply(s, dt)` while precision-mode active | All gains scaled by `PrecisionScale` (default 0.2) |
| `Apply` with Button1 newly pressed (rising edge) | `accRot = Identity` |
| `Apply` with Button2 newly pressed (rising edge) | Toggle precision mode |
| `OnRelease()` | Zero `accRot` and `accOffset` |

**Files:**
- Create: `src/SpaceMouseRepo.Core/Behavior/ManipulationConfig.cs`
- Create: `src/SpaceMouseRepo.Core/Behavior/ButtonAction.cs`
- Create: `src/SpaceMouseRepo.Core/Behavior/HeldObjectController.cs`
- Create: `src/SpaceMouseRepo.Tests/Behavior/HeldObjectControllerTests.cs`

- [ ] **Step 1: Create the config POCO and the action enum**

Create `src/SpaceMouseRepo.Core/Behavior/ButtonAction.cs`:

```csharp
namespace SpaceMouseRepo.Core.Behavior;

public enum ButtonAction
{
    None,
    ResetRotation,
    TogglePrecisionMode,
}
```

Create `src/SpaceMouseRepo.Core/Behavior/ManipulationConfig.cs`:

```csharp
namespace SpaceMouseRepo.Core.Behavior;

public sealed class ManipulationConfig
{
    public float RotationDegPerSec { get; set; } = 180f;
    public float TranslationMPerSec { get; set; } = 0.30f;
    public float MaxOffsetM { get; set; } = 0.15f;
    public float PrecisionScale { get; set; } = 0.2f;

    public bool InvertTx { get; set; }
    public bool InvertTy { get; set; }
    public bool InvertTz { get; set; }
    public bool InvertRx { get; set; }
    public bool InvertRy { get; set; }
    public bool InvertRz { get; set; }

    public ButtonAction Button1Action { get; set; } = ButtonAction.ResetRotation;
    public ButtonAction Button2Action { get; set; } = ButtonAction.TogglePrecisionMode;
}
```

- [ ] **Step 2: Write the failing tests**

Create `src/SpaceMouseRepo.Tests/Behavior/HeldObjectControllerTests.cs`:

```csharp
using System;
using System.Numerics;
using SpaceMouseRepo.Core.Behavior;
using SpaceMouseRepo.Core.Input;
using Xunit;

namespace SpaceMouseRepo.Tests.Behavior;

public class HeldObjectControllerTests
{
    private static SpaceMouseState Axes(float tx = 0, float ty = 0, float tz = 0,
                                        float rx = 0, float ry = 0, float rz = 0,
                                        bool b1 = false, bool b2 = false)
        => new(tx, ty, tz, rx, ry, rz, b1, b2);

    [Fact]
    public void Initial_accumulators_are_identity_and_zero()
    {
        var c = new HeldObjectController(new ManipulationConfig());
        Assert.Equal(Quaternion.Identity, c.AccumulatedRotation);
        Assert.Equal(Vector3.Zero, c.AccumulatedOffset);
        Assert.False(c.PrecisionModeActive);
    }

    [Fact]
    public void Rotation_axis_input_accumulates_about_y()
    {
        var cfg = new ManipulationConfig { RotationDegPerSec = 90f };
        var c = new HeldObjectController(cfg);
        c.Apply(Axes(ry: 1f), dt: 1f); // 90 deg about Y
        var euler = ToEulerY(c.AccumulatedRotation);
        Assert.InRange(euler, 89f, 91f);
    }

    [Fact]
    public void Translation_accumulates_then_clamps_to_max_radius()
    {
        var cfg = new ManipulationConfig { TranslationMPerSec = 1f, MaxOffsetM = 0.5f };
        var c = new HeldObjectController(cfg);
        for (int i = 0; i < 10; i++)
            c.Apply(Axes(tx: 1f), dt: 0.1f); // would accumulate to 1m without clamp
        Assert.Equal(0.5f, c.AccumulatedOffset.X, 3);
        Assert.Equal(0.5f, c.AccumulatedOffset.Length(), 3);
    }

    [Fact]
    public void Axis_inversion_flips_sign()
    {
        var cfg = new ManipulationConfig
        {
            TranslationMPerSec = 1f, MaxOffsetM = 1f,
            InvertTx = true,
        };
        var c = new HeldObjectController(cfg);
        c.Apply(Axes(tx: 1f), dt: 0.1f);
        Assert.Equal(-0.1f, c.AccumulatedOffset.X, 3);
    }

    [Fact]
    public void OnRelease_zeros_accumulators()
    {
        var c = new HeldObjectController(new ManipulationConfig());
        c.Apply(Axes(rx: 1f, tx: 1f), dt: 0.1f);
        c.OnRelease();
        Assert.Equal(Quaternion.Identity, c.AccumulatedRotation);
        Assert.Equal(Vector3.Zero, c.AccumulatedOffset);
    }

    [Fact]
    public void Button1_rising_edge_resets_rotation_only()
    {
        var c = new HeldObjectController(new ManipulationConfig());
        c.Apply(Axes(rx: 1f, tx: 1f), dt: 0.1f);
        var offsetBefore = c.AccumulatedOffset;
        c.Apply(Axes(b1: true), dt: 0.0f);
        Assert.Equal(Quaternion.Identity, c.AccumulatedRotation);
        Assert.Equal(offsetBefore, c.AccumulatedOffset);
    }

    [Fact]
    public void Button1_held_does_not_reset_repeatedly()
    {
        var c = new HeldObjectController(new ManipulationConfig());
        c.Apply(Axes(b1: true), dt: 0.0f);   // rising edge, reset (already identity)
        c.Apply(Axes(rx: 1f, b1: true), dt: 0.1f);
        // Button still held, no reset on this frame; rotation should accumulate.
        Assert.NotEqual(Quaternion.Identity, c.AccumulatedRotation);
    }

    [Fact]
    public void Button2_rising_edge_toggles_precision_mode()
    {
        var c = new HeldObjectController(new ManipulationConfig());
        Assert.False(c.PrecisionModeActive);
        c.Apply(Axes(b2: true), dt: 0f);
        Assert.True(c.PrecisionModeActive);
        c.Apply(Axes(b2: false), dt: 0f);
        Assert.True(c.PrecisionModeActive);  // toggle, not momentary
        c.Apply(Axes(b2: true), dt: 0f);
        Assert.False(c.PrecisionModeActive);
    }

    [Fact]
    public void Precision_mode_scales_gains()
    {
        var cfg = new ManipulationConfig
        {
            TranslationMPerSec = 1f, MaxOffsetM = 1f,
            PrecisionScale = 0.2f,
            Button2Action = ButtonAction.TogglePrecisionMode,
        };
        var c = new HeldObjectController(cfg);
        c.Apply(Axes(b2: true), dt: 0f);
        c.Apply(Axes(tx: 1f), dt: 0.1f);
        Assert.Equal(0.02f, c.AccumulatedOffset.X, 3);
    }

    [Fact]
    public void Button_action_None_disables_button()
    {
        var cfg = new ManipulationConfig { Button1Action = ButtonAction.None };
        var c = new HeldObjectController(cfg);
        c.Apply(Axes(rx: 1f), dt: 0.1f);
        var rotBefore = c.AccumulatedRotation;
        c.Apply(Axes(b1: true), dt: 0f);
        Assert.Equal(rotBefore, c.AccumulatedRotation);
    }

    private static float ToEulerY(Quaternion q)
    {
        // Extract Y rotation in degrees from a quaternion that represents rotation about Y only.
        double sin = 2.0 * (q.W * q.Y + q.X * q.Z);
        double cos = 1.0 - 2.0 * (q.Y * q.Y + q.X * q.X);
        return (float)(Math.Atan2(sin, cos) * 180.0 / Math.PI);
    }
}
```

- [ ] **Step 3: Run, confirm failures**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: 10 failed (`HeldObjectController` does not exist).

- [ ] **Step 4: Write the implementation**

Create `src/SpaceMouseRepo.Core/Behavior/HeldObjectController.cs`:

```csharp
using System;
using System.Numerics;
using SpaceMouseRepo.Core.Input;

namespace SpaceMouseRepo.Core.Behavior;

public sealed class HeldObjectController
{
    private const float DegToRad = (float)(Math.PI / 180.0);

    private readonly ManipulationConfig _cfg;
    private Quaternion _accRot = Quaternion.Identity;
    private Vector3 _accOffset = Vector3.Zero;
    private bool _b1Prev;
    private bool _b2Prev;
    private bool _precision;

    public HeldObjectController(ManipulationConfig cfg) { _cfg = cfg; }

    public Quaternion AccumulatedRotation => _accRot;
    public Vector3 AccumulatedOffset => _accOffset;
    public bool PrecisionModeActive => _precision;

    public void OnRelease()
    {
        _accRot = Quaternion.Identity;
        _accOffset = Vector3.Zero;
    }

    public void Apply(SpaceMouseState s, float dt)
    {
        HandleButtonEdge(s.Button1, ref _b1Prev, _cfg.Button1Action);
        HandleButtonEdge(s.Button2, ref _b2Prev, _cfg.Button2Action);

        float scale = _precision ? _cfg.PrecisionScale : 1f;
        float rotSens = _cfg.RotationDegPerSec * scale;
        float transSens = _cfg.TranslationMPerSec * scale;

        float rx = (_cfg.InvertRx ? -s.Rx : s.Rx) * rotSens * dt * DegToRad;
        float ry = (_cfg.InvertRy ? -s.Ry : s.Ry) * rotSens * dt * DegToRad;
        float rz = (_cfg.InvertRz ? -s.Rz : s.Rz) * rotSens * dt * DegToRad;

        if (rx != 0f || ry != 0f || rz != 0f)
        {
            var delta = Quaternion.CreateFromYawPitchRoll(ry, rx, rz);
            _accRot = Quaternion.Normalize(delta * _accRot);
        }

        var deltaOffset = new Vector3(
            (_cfg.InvertTx ? -s.Tx : s.Tx),
            (_cfg.InvertTy ? -s.Ty : s.Ty),
            (_cfg.InvertTz ? -s.Tz : s.Tz)) * transSens * dt;
        _accOffset += deltaOffset;
        if (_accOffset.Length() > _cfg.MaxOffsetM)
            _accOffset = Vector3.Normalize(_accOffset) * _cfg.MaxOffsetM;
    }

    private void HandleButtonEdge(bool current, ref bool prev, ButtonAction action)
    {
        bool rising = current && !prev;
        prev = current;
        if (!rising) return;
        switch (action)
        {
            case ButtonAction.ResetRotation:
                _accRot = Quaternion.Identity;
                break;
            case ButtonAction.TogglePrecisionMode:
                _precision = !_precision;
                break;
            case ButtonAction.None:
                break;
        }
    }
}
```

- [ ] **Step 5: Run, confirm pass**

Run: `dotnet test src/SpaceMouseRepo.sln`
Expected: `Passed: 20, Failed: 0` (2 + 8 + 10).

- [ ] **Step 6: Commit**

```bash
git add src/SpaceMouseRepo.Core/Behavior/ src/SpaceMouseRepo.Tests/Behavior/
git commit -m "feat(core): HeldObjectController accumulators with precision mode"
```

---

## Task 5: `SpaceMouseHid` — HidLibrary device discovery + background read loop (plugin)

Lives in the plugin project because it depends on `HidLibrary` which is Windows-runtime-only. Wraps the parser. Owns one background thread that reads HID reports and calls `parser.Feed(...)`. Exposes `Current.State` on the main thread without locks (volatile reference swap).

This task has limited unit testability — it's I/O against real hardware. We test what we can (the parser is already covered) and rely on the manual smoke tests in Task 11.

**Files:**
- Create: `src/SpaceMouseRepo.Plugin/Input/SpaceMouseHid.cs`

- [ ] **Step 1: Create the file**

Create `src/SpaceMouseRepo.Plugin/Input/SpaceMouseHid.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using BepInEx.Logging;
using HidLibrary;
using SpaceMouseRepo.Core.Input;

namespace SpaceMouseRepo.Input;

public sealed class SpaceMouseHid : IDisposable
{
    private const int VendorId = 0x256F;
    private static readonly int[] DefaultProductIds = { 0xC62E, 0xC62F, 0xC652 };

    private readonly ManualLogSource _log;
    private readonly SpaceMouseReportParser _parser;
    private readonly HidDevice? _device;
    private readonly Thread? _readThread;
    private volatile bool _running;

    public SpaceMouseHid(ManualLogSource log, IEnumerable<int> extraProductIds, float translationDeadzone, float rotationDeadzone)
    {
        _log = log;
        _parser = new SpaceMouseReportParser(translationDeadzone, rotationDeadzone);

        var allowed = new HashSet<int>(DefaultProductIds);
        foreach (var p in extraProductIds) allowed.Add(p);

        var found = HidDevices.Enumerate(VendorId).ToList();
        if (found.Count == 0)
        {
            _log.LogWarning($"No 3Dconnexion HID devices found (vendor 0x{VendorId:X4}). Plugin will be inactive.");
            return;
        }

        // Diagnostic: log every 3Dconnexion device, even non-matching ones — helps users add new ProductIds via config.
        foreach (var dev in found)
            _log.LogInfo($"Found 3Dconnexion HID device: VID=0x{dev.Attributes.VendorId:X4} PID=0x{dev.Attributes.ProductId:X4} {dev.Description}");

        _device = found.FirstOrDefault(d => allowed.Contains(d.Attributes.ProductId));
        if (_device == null)
        {
            _log.LogWarning("3Dconnexion device(s) found but none matched known SpaceMouse product IDs. Add the PID to ExtraProductIds in config.");
            return;
        }

        _device.OpenDevice();
        _running = true;
        _readThread = new Thread(ReadLoop) { IsBackground = true, Name = "SpaceMouseHidRead" };
        _readThread.Start();
        _log.LogInfo($"Opened SpaceMouse PID=0x{_device.Attributes.ProductId:X4}");
    }

    public SpaceMouseState State => _parser.State;
    public bool IsActive => _device != null && _device.IsOpen && _running;

    private void ReadLoop()
    {
        while (_running && _device != null && _device.IsConnected)
        {
            var report = _device.ReadReport(timeout: 100);
            if (report.Status == HidDeviceData.ReadStatus.Success && report.Data.Length > 0)
            {
                // HidLibrary strips the leading report ID into ReportId; reinsert for the parser.
                var bytes = new byte[report.Data.Length + 1];
                bytes[0] = report.ReportId;
                Buffer.BlockCopy(report.Data, 0, bytes, 1, report.Data.Length);
                _parser.Feed(bytes);
            }
        }
    }

    public void Dispose()
    {
        _running = false;
        _readThread?.Join(500);
        if (_device != null && _device.IsOpen) _device.CloseDevice();
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/SpaceMouseRepo.sln`
Expected: `Build succeeded` for all three projects.

- [ ] **Step 3: Commit**

```bash
git add src/SpaceMouseRepo.Plugin/Input/
git commit -m "feat(plugin): SpaceMouseHid device discovery + read loop"
```

---

## Task 6: `Config` — bind BepInEx ConfigFile entries to `ManipulationConfig` + extra product IDs

Reads BepInEx config on plugin load, populates a `ManipulationConfig` plus an `ExtraProductIds` list. Re-reads on config-file change so live edits don't require a restart for sensitivity tweaking.

**Files:**
- Create: `src/SpaceMouseRepo.Plugin/Config/PluginConfig.cs`

- [ ] **Step 1: Create the file**

Create `src/SpaceMouseRepo.Plugin/Config/PluginConfig.cs`:

```csharp
using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;
using SpaceMouseRepo.Core.Behavior;

namespace SpaceMouseRepo.Config;

public sealed class PluginConfig
{
    public ManipulationConfig Manipulation { get; }
    public IReadOnlyList<int> ExtraProductIds => _extraIds;
    public float TranslationDeadzone => _tDead.Value;
    public float RotationDeadzone => _rDead.Value;

    private readonly ConfigEntry<float> _rotDeg, _transCm, _maxOffsetCm, _precScale, _tDead, _rDead;
    private readonly ConfigEntry<bool> _iTx, _iTy, _iTz, _iRx, _iRy, _iRz;
    private readonly ConfigEntry<ButtonAction> _b1, _b2;
    private readonly ConfigEntry<string> _extra;
    private List<int> _extraIds = new();

    public PluginConfig(ConfigFile cf)
    {
        _rotDeg     = cf.Bind("Sensitivity", "RotationDegPerSec",   180f, "Degrees per second of rotation at full puck deflection.");
        _transCm    = cf.Bind("Sensitivity", "TranslationCmPerSec",  30f, "Centimeters per second of local offset at full puck deflection.");
        _maxOffsetCm= cf.Bind("Sensitivity", "MaxLocalOffsetCm",     15f, "Maximum local-offset radius in centimeters.");
        _precScale  = cf.Bind("Sensitivity", "PrecisionScale",      0.2f, "Multiplier applied to all gains when precision mode is active.");

        _tDead      = cf.Bind("Deadzone",    "Translation",        0.05f, "Translation axis deadzone (0-1, fraction of full deflection).");
        _rDead      = cf.Bind("Deadzone",    "Rotation",           0.05f, "Rotation axis deadzone.");

        _iTx = cf.Bind("AxisInversion", "InvertTx", false, "");
        _iTy = cf.Bind("AxisInversion", "InvertTy", false, "");
        _iTz = cf.Bind("AxisInversion", "InvertTz", false, "");
        _iRx = cf.Bind("AxisInversion", "InvertRx", false, "");
        _iRy = cf.Bind("AxisInversion", "InvertRy", false, "");
        _iRz = cf.Bind("AxisInversion", "InvertRz", false, "");

        _b1 = cf.Bind("Bindings", "Button1", ButtonAction.ResetRotation,       "Action for SpaceMouse Button 1.");
        _b2 = cf.Bind("Bindings", "Button2", ButtonAction.TogglePrecisionMode, "Action for SpaceMouse Button 2.");

        _extra = cf.Bind("Hardware", "ExtraProductIds", "",
            "Comma-separated extra HID product IDs in hex (e.g. 0xC631,0xC632) for SpaceMouse models not yet recognized by default.");

        Manipulation = new ManipulationConfig();
        Refresh();
        cf.SettingChanged += (_, __) => Refresh();
    }

    private void Refresh()
    {
        Manipulation.RotationDegPerSec  = _rotDeg.Value;
        Manipulation.TranslationMPerSec = _transCm.Value / 100f;
        Manipulation.MaxOffsetM         = _maxOffsetCm.Value / 100f;
        Manipulation.PrecisionScale     = _precScale.Value;
        Manipulation.InvertTx = _iTx.Value;
        Manipulation.InvertTy = _iTy.Value;
        Manipulation.InvertTz = _iTz.Value;
        Manipulation.InvertRx = _iRx.Value;
        Manipulation.InvertRy = _iRy.Value;
        Manipulation.InvertRz = _iRz.Value;
        Manipulation.Button1Action = _b1.Value;
        Manipulation.Button2Action = _b2.Value;

        _extraIds = _extra.Value
            .Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim())
            .Select(s => s.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? s.Substring(2) : s)
            .Select(s => int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out var v) ? v : -1)
            .Where(v => v > 0)
            .ToList();
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/SpaceMouseRepo.sln`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/SpaceMouseRepo.Plugin/Config/
git commit -m "feat(plugin): PluginConfig binds BepInEx settings to ManipulationConfig"
```

---

## Task 7: `GrabPatches` — Harmony postfix on `PhysGrabber`

Best-guess hook target: `PhysGrabber.Update`. Defensive: if the type or method isn't found, log clearly and stay dormant rather than crash. Reads vanilla target-rotation/position from the held `PhysGrabObject`'s rigidbody and rotates+offsets it before the next physics step. Per-grab accumulators are keyed by holder via a `ConditionalWeakTable<PhysGrabber, HeldObjectController>` so multi-player (hot-seat-style spectators) doesn't cross the streams.

**Field-name guesses** (will need adjustment after first runtime test — listed in Risks):
- `PhysGrabber.isLocal` (bool) — am I the local player?
- `PhysGrabber.grabbed` (PhysGrabObject?) — currently held object, null when not holding
- `PhysGrabObject.targetRotation` (UnityEngine.Quaternion) — vanilla per-frame target the rigidbody is steered toward
- `PhysGrabObject.targetPosition` (UnityEngine.Vector3) — vanilla per-frame target

If any of these names are wrong at runtime, the Harmony patch will throw at first invocation; we log the exception and disable the patch for the rest of the session. The user posts the log; we adjust the names in a v0.0.2.

**Files:**
- Create: `src/SpaceMouseRepo.Plugin/Patches/GrabPatches.cs`

- [ ] **Step 1: Create the file**

Create `src/SpaceMouseRepo.Plugin/Patches/GrabPatches.cs`:

```csharp
using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using BepInEx.Logging;
using HarmonyLib;
using SpaceMouseRepo.Core.Behavior;
using SpaceMouseRepo.Core.Input;
using SNVector3 = System.Numerics.Vector3;
using SNQuaternion = System.Numerics.Quaternion;
using UVector3 = UnityEngine.Vector3;
using UQuaternion = UnityEngine.Quaternion;

namespace SpaceMouseRepo.Patches;

public static class GrabPatches
{
    private static ManualLogSource _log = null!;
    private static ManipulationConfig _cfg = null!;
    private static Func<SpaceMouseState> _readState = () => SpaceMouseState.Empty;
    private static bool _disabled;
    private static readonly ConditionalWeakTable<object, HeldObjectController> _byHolder = new();

    private static FieldInfo? _isLocalField;
    private static FieldInfo? _grabbedField;
    private static FieldInfo? _targetRotField;
    private static FieldInfo? _targetPosField;

    public static void Install(Harmony harmony, ManualLogSource log, ManipulationConfig cfg, Func<SpaceMouseState> readState)
    {
        _log = log;
        _cfg = cfg;
        _readState = readState;

        var grabberType = AccessTools.TypeByName("PhysGrabber");
        if (grabberType == null)
        {
            _log.LogError("Type PhysGrabber not found. SpaceMouse mod inactive. Report this with the BepInEx log.");
            _disabled = true;
            return;
        }

        var update = AccessTools.Method(grabberType, "Update")
                  ?? AccessTools.Method(grabberType, "LateUpdate");
        if (update == null)
        {
            _log.LogError("PhysGrabber.Update / LateUpdate not found. SpaceMouse mod inactive.");
            _disabled = true;
            return;
        }

        _isLocalField  = AccessTools.Field(grabberType, "isLocal");
        _grabbedField  = AccessTools.Field(grabberType, "grabbed");
        if (_isLocalField == null || _grabbedField == null)
        {
            _log.LogError($"PhysGrabber field discovery failed: isLocal={_isLocalField != null} grabbed={_grabbedField != null}. Plugin inactive.");
            _disabled = true;
            return;
        }

        var postfix = new HarmonyMethod(typeof(GrabPatches).GetMethod(nameof(PostUpdate), BindingFlags.Static | BindingFlags.NonPublic));
        harmony.Patch(update, postfix: postfix);
        _log.LogInfo($"Patched {grabberType.FullName}.{update.Name}");
    }

    private static void PostUpdate(object __instance)
    {
        if (_disabled) return;
        try
        {
            if (_isLocalField!.GetValue(__instance) is not bool isLocal || !isLocal) return;
            var grabbed = _grabbedField!.GetValue(__instance);
            var ctrl = _byHolder.GetValue(__instance, _ => new HeldObjectController(_cfg));

            if (grabbed == null)
            {
                ctrl.OnRelease();
                return;
            }

            EnsureTargetFields(grabbed);
            if (_targetRotField == null || _targetPosField == null) return;

            ctrl.Apply(_readState(), UnityEngine.Time.deltaTime);

            var rot = (UQuaternion)_targetRotField.GetValue(grabbed)!;
            var pos = (UVector3)_targetPosField.GetValue(grabbed)!;

            var addRot = ToUnity(ctrl.AccumulatedRotation);
            var addPos = ToUnity(ctrl.AccumulatedOffset);

            _targetRotField.SetValue(grabbed, addRot * rot);
            _targetPosField.SetValue(grabbed, pos + addPos);
        }
        catch (Exception e)
        {
            _log.LogError($"GrabPatches.PostUpdate threw, disabling for session: {e}");
            _disabled = true;
        }
    }

    private static void EnsureTargetFields(object grabbed)
    {
        if (_targetRotField != null && _targetPosField != null) return;
        var t = grabbed.GetType();
        _targetRotField ??= AccessTools.Field(t, "targetRotation");
        _targetPosField ??= AccessTools.Field(t, "targetPosition");
        if (_targetRotField == null || _targetPosField == null)
            _log.LogError($"PhysGrabObject target fields not found on {t.FullName}. Looked for: targetRotation, targetPosition.");
    }

    private static UQuaternion ToUnity(SNQuaternion q) => new(q.X, q.Y, q.Z, q.W);
    private static UVector3 ToUnity(SNVector3 v) => new(v.X, v.Y, v.Z);
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/SpaceMouseRepo.sln`
Expected: `Build succeeded`.

- [ ] **Step 3: Commit**

```bash
git add src/SpaceMouseRepo.Plugin/Patches/
git commit -m "feat(plugin): GrabPatches Harmony postfix with defensive field lookup"
```

---

## Task 8: `Plugin.cs` — BepInEx entry, wires everything together

Single class with `[BepInPlugin]` attribute. On `Awake`: instantiate config, HID, install patches, store state-reader closure. On `OnDestroy`: dispose HID.

**Files:**
- Create: `src/SpaceMouseRepo.Plugin/Plugin.cs`

- [ ] **Step 1: Create the file**

Create `src/SpaceMouseRepo.Plugin/Plugin.cs`:

```csharp
using BepInEx;
using HarmonyLib;
using SpaceMouseRepo.Config;
using SpaceMouseRepo.Input;
using SpaceMouseRepo.Patches;

namespace SpaceMouseRepo;

[BepInPlugin(GUID, NAME, VERSION)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string GUID = "com.zanewebb.spacemouse_repo";
    public const string NAME = "SpaceMouse for R.E.P.O.";
    public const string VERSION = "0.1.0";

    private SpaceMouseHid? _hid;
    private Harmony? _harmony;

    private void Awake()
    {
        Logger.LogInfo($"{NAME} v{VERSION} loading…");
        var pcfg = new PluginConfig(Config);

        _hid = new SpaceMouseHid(Logger, pcfg.ExtraProductIds, pcfg.TranslationDeadzone, pcfg.RotationDeadzone);

        _harmony = new Harmony(GUID);
        GrabPatches.Install(_harmony, Logger, pcfg.Manipulation, () => _hid?.State ?? Core.Input.SpaceMouseState.Empty);

        Logger.LogInfo($"{NAME} ready. HID active: {_hid.IsActive}");
    }

    private void OnDestroy()
    {
        _harmony?.UnpatchSelf();
        _hid?.Dispose();
    }
}
```

- [ ] **Step 2: Verify build produces the plugin DLL**

Run: `dotnet build src/SpaceMouseRepo.Plugin -c Release`
Expected: `Build succeeded`. Verify DLL exists:

Run: `ls -la src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.dll`
Expected: file present, ~30–60 KB.

- [ ] **Step 3: Commit**

```bash
git add src/SpaceMouseRepo.Plugin/Plugin.cs
git commit -m "feat(plugin): Plugin entry wires Hid + Patches + Config"
```

---

## Task 9: Thunderstore packaging — manifest, icon, README, packaging script

Thunderstore packages are zip files containing `manifest.json`, `icon.png` (256×256), `README.md`, and the plugin DLL(s) under a `BepInEx/plugins/<modname>/` layout. r2modman installs from that zip.

**Files:**
- Create: `thunderstore/manifest.json`
- Create: `thunderstore/icon.png` (placeholder; user replaces with art)
- Create: `thunderstore/README.md`
- Create: `scripts/package.sh`

- [ ] **Step 1: Create `thunderstore/manifest.json`**

```json
{
  "name": "SpaceMouseRepo",
  "version_number": "0.1.0",
  "website_url": "https://github.com/zanewebb/SpaceMouseRepo",
  "description": "Use a 3Dconnexion SpaceMouse to rotate and finely position held objects.",
  "dependencies": [
    "BepInEx-BepInExPack-5.4.2100"
  ]
}
```

- [ ] **Step 2: Create `thunderstore/icon.png`** placeholder (256×256, any solid-color PNG works for development; replace before public release).

Generate with ImageMagick if installed:
```
magick -size 256x256 canvas:#1f6feb thunderstore/icon.png
```
Or copy any 256×256 PNG to that path. Confirm: `file thunderstore/icon.png` reports `PNG image data, 256 x 256`.

- [ ] **Step 3: Create `thunderstore/README.md`**

```markdown
# SpaceMouseRepo

Adds 3Dconnexion SpaceMouse support to R.E.P.O. While holding an object, the SpaceMouse puck rotates it (full 6DOF) and applies a small clamped local offset. Mouse aim and scroll-wheel depth keep their vanilla roles. Camera is never touched.

## Requirements

- A SpaceMouse Wireless / Wireless Compact (or any 3Dconnexion HID device that uses the standard report layout).
- BepInExPack 5.4.21+.

## Multiplayer

Client-side only. Only the player using the SpaceMouse needs the mod installed; other clients receive normal networked transform updates.

## Configuration

Edit `BepInEx/config/com.zanewebb.spacemouse_repo.cfg` after first run. Key settings:

- `RotationDegPerSec` — rotation speed at full puck deflection (default 180°/s).
- `TranslationCmPerSec` — translation speed at full puck deflection (default 30 cm/s).
- `MaxLocalOffsetCm` — radius limit on accumulated local offset (default 15 cm).
- `PrecisionScale` — gain reduction when precision mode is active (default 0.2 = 5× finer).
- `Button1` / `Button2` — actions: `ResetRotation`, `TogglePrecisionMode`, or `None`.
- `ExtraProductIds` — comma-separated hex IDs if your model isn't recognized.

## Troubleshooting

If your SpaceMouse isn't detected, the BepInEx log will list every 3Dconnexion HID device it found, with its product ID. Paste the matching ID into `ExtraProductIds`.
```

- [ ] **Step 4: Create `scripts/package.sh` (POSIX shell, runs on macOS)**

```bash
#!/usr/bin/env bash
set -euo pipefail
cd "$(dirname "$0")/.."

VERSION=$(grep -m1 'version_number' thunderstore/manifest.json | sed -E 's/.*"version_number": *"([^"]+)".*/\1/')
OUT="dist/SpaceMouseRepo-${VERSION}.zip"

dotnet build src/SpaceMouseRepo.Plugin -c Release

STAGE="$(mktemp -d)"
trap "rm -rf '$STAGE'" EXIT

mkdir -p "$STAGE/BepInEx/plugins/SpaceMouseRepo"
cp src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.dll       "$STAGE/BepInEx/plugins/SpaceMouseRepo/"
cp src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.Core.dll  "$STAGE/BepInEx/plugins/SpaceMouseRepo/"
cp src/SpaceMouseRepo.Plugin/bin/Release/net472/HidLibrary.dll           "$STAGE/BepInEx/plugins/SpaceMouseRepo/"

cp thunderstore/manifest.json "$STAGE/"
cp thunderstore/icon.png      "$STAGE/"
cp thunderstore/README.md     "$STAGE/"

mkdir -p dist
(cd "$STAGE" && zip -qr "$OLDPWD/$OUT" .)
echo "Wrote $OUT"
```

Make executable:
```
chmod +x scripts/package.sh
```

- [ ] **Step 5: Run packaging end-to-end**

Run: `scripts/package.sh`
Expected: `Wrote dist/SpaceMouseRepo-0.1.0.zip`. Verify zip contents:
```
unzip -l dist/SpaceMouseRepo-0.1.0.zip
```
Expected entries: `manifest.json`, `icon.png`, `README.md`, `BepInEx/plugins/SpaceMouseRepo/SpaceMouseRepo.dll`, `SpaceMouseRepo.Core.dll`, `HidLibrary.dll`.

- [ ] **Step 6: Add `dist/` to .gitignore**

Append to `.gitignore`:
```
dist/
```

- [ ] **Step 7: Commit**

```bash
git add thunderstore/ scripts/ .gitignore
git commit -m "build: Thunderstore packaging script and metadata"
```

---

## Task 10: GitHub Actions Windows build

Builds the plugin on a Windows runner per push, uploads the packaged zip as an artifact, and creates a release on tagged commits. The user can pull artifacts straight to a Windows test machine without needing to keep a local Windows toolchain.

**Files:**
- Create: `.github/workflows/build.yml`

- [ ] **Step 1: Create the workflow**

```yaml
name: Build

on:
  push:
    branches: [main]
    tags: ['v*']
  pull_request:

jobs:
  build:
    runs-on: windows-latest
    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '8.0.x'

      - name: Restore
        run: dotnet restore src/SpaceMouseRepo.sln

      - name: Test
        run: dotnet test src/SpaceMouseRepo.sln --no-restore --verbosity normal

      - name: Build plugin (Release)
        run: dotnet build src/SpaceMouseRepo.Plugin -c Release --no-restore

      - name: Stage Thunderstore package
        shell: pwsh
        run: |
          $version = (Get-Content thunderstore/manifest.json | ConvertFrom-Json).version_number
          $stage = "stage"
          $pluginDir = "$stage/BepInEx/plugins/SpaceMouseRepo"
          New-Item -ItemType Directory -Force -Path $pluginDir | Out-Null
          Copy-Item src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.dll      $pluginDir
          Copy-Item src/SpaceMouseRepo.Plugin/bin/Release/net472/SpaceMouseRepo.Core.dll $pluginDir
          Copy-Item src/SpaceMouseRepo.Plugin/bin/Release/net472/HidLibrary.dll          $pluginDir
          Copy-Item thunderstore/manifest.json $stage
          Copy-Item thunderstore/icon.png      $stage
          Copy-Item thunderstore/README.md     $stage
          Compress-Archive -Path "$stage/*" -DestinationPath "SpaceMouseRepo-$version.zip" -Force
          Write-Output "Built SpaceMouseRepo-$version.zip"

      - uses: actions/upload-artifact@v4
        with:
          name: thunderstore-package
          path: SpaceMouseRepo-*.zip

      - name: Create Release
        if: startsWith(github.ref, 'refs/tags/v')
        uses: softprops/action-gh-release@v2
        with:
          files: SpaceMouseRepo-*.zip
```

- [ ] **Step 2: Commit**

```bash
git add .github/workflows/build.yml
git commit -m "ci: Windows build + test + Thunderstore package artifact"
```

- [ ] **Step 3 (deferred — at user's discretion): push to GitHub**

This is a destructive-ish action (pushing to a remote, creating a public-ish artifact). Do NOT run automatically:

```bash
# Run manually when ready:
# git remote add origin git@github.com:zanewebb/SpaceMouseRepo.git
# git push -u origin main
```

---

## Task 11: Manual smoke-test checklist (Windows host required)

This task is NOT TDD — it's a checklist the user runs on a Windows machine with R.E.P.O. installed, the SpaceMouse plugged in, and r2modman pointed at the local zip.

**Setup steps (one time):**

- [ ] **Step 1:** Install r2modman from thunderstore.io. Add the R.E.P.O. game profile.

- [ ] **Step 2:** In r2modman, **Settings → Browse profile folder**. Note the path (typically `%APPDATA%/r2modmanPlus-local/REPO/profiles/Default/`).

- [ ] **Step 3:** In r2modman, install **BepInExPack** (latest 5.4.x) into the active profile. Run the game once via r2modman to let BepInEx unpack itself; close after main menu reaches.

- [ ] **Step 4:** Copy `dist/SpaceMouseRepo-0.1.0.zip` from the GitHub Actions artifact (or from your dev machine) to the Windows host. In r2modman, **Online → Local → Import local mod →** choose the zip. Confirm the mod appears in the installed list.

- [ ] **Step 5:** Plug in the SpaceMouse Wireless. Start the game via r2modman.

**Verification — capture the BepInEx log after each:**

Log path: `<r2modman profile>/BepInEx/LogOutput.log`.

- [ ] **Test 1 (cold start, device attached):** Open the log. Search for "SpaceMouse for R.E.P.O. v0.1.0 loading". Confirm a line `Opened SpaceMouse PID=0x...` appears.

  - If instead you see `3Dconnexion device(s) found but none matched`, copy the `Found 3Dconnexion HID device: VID=0x256F PID=0x????` line, set `ExtraProductIds = 0x????` in `BepInEx/config/com.zanewebb.spacemouse_repo.cfg`, restart the game, retry Test 1.

- [ ] **Test 2 (device unplugged):** Quit, unplug SpaceMouse, restart. Log should contain `No 3Dconnexion HID devices found … Plugin will be inactive.` Game plays normally with no SpaceMouse effect. Replug SpaceMouse afterwards.

- [ ] **Test 3 (Harmony hook):** Start a single-player run. Log should contain `Patched PhysGrabber.Update` (or `LateUpdate`). If instead you see `Type PhysGrabber not found` or `PhysGrabber.Update / LateUpdate not found`, copy that error line — the hook target needs to be adjusted in `GrabPatches.Install`.

- [ ] **Test 4 (rotation works):** Pick up a small object (a coffee cup, fragile valuable). Tilt the SpaceMouse puck about its horizontal axis. The held object should pitch. Repeat for all three rotation axes. Released object should retain rotation while physics takes over.

- [ ] **Test 5 (translation works):** Pick up an object in front of a shelf. Push the puck sideways without rotating. The object should slide laterally up to the clamp distance (~15 cm) and stop. Pull the puck back to center, then push the other way — confirm the offset moves through zero and to the other side.

- [ ] **Test 6 (Button 1 — reset rotation):** After Test 4, with object still held, press SpaceMouse Button 1 once. The object's rotation should snap back to its vanilla target. Hold Button 1 down while rotating with the puck — rotation should still accumulate (not stuck at identity).

- [ ] **Test 7 (Button 2 — precision mode):** Press Button 2. Tilt the puck at the same magnitude as Test 4. Rotation should be ~5× slower. Press Button 2 again to disable.

- [ ] **Test 8 (multiplayer host's view):** Host a 2-player lobby with a friend on vanilla. Hold an object, rotate it via SpaceMouse. Friend should see your manipulations live (their networked-grab visualization should match yours).

- [ ] **Test 9 (multiplayer non-host):** Join a lobby as client (other player hosting). Hold an object, rotate via SpaceMouse. Confirm the host sees your manipulations the same as in Test 8.

- [ ] **Test 10 (PostUpdate exception path):** This is opportunistic — if any test throws, the log will contain `GrabPatches.PostUpdate threw, disabling for session: …` followed by a stack trace. Capture it; it tells us which field name was wrong. The plugin should not crash the game; subsequent grabs are pure-vanilla.

**On any test failure:** save the relevant LogOutput.log lines, paste them into a follow-up plan or issue. Common adjustments:
- Wrong patch target → change `AccessTools.Method(grabberType, "Update")` to the actual method name observed in the decompile.
- Wrong field name on `PhysGrabber` (`isLocal`/`grabbed`) or `PhysGrabObject` (`targetRotation`/`targetPosition`) → adjust the `AccessTools.Field` calls in `GrabPatches`.
- Object jitters violently → reduce `RotationDegPerSec` in config. If jitter persists with low gain, the vanilla code likely overwrites our values within the same frame — this is the case where postfix is insufficient and we need a transpiler patch (a v0.0.2 task; out of scope for this plan).

---

## Self-review notes (post-write)

Spec coverage:
- Manipulation model (mouse/scroll vanilla; SpaceMouse rotation 6DOF + clamped offset) → Tasks 4 (math), 7 (patch).
- HID parsing per spec layout → Task 3.
- Config layout per spec → Task 6.
- Multiplayer client-only behavior → Task 7 (`isLocal` gate), Task 11 tests 8-9 verify.
- Graceful no-op on missing device → Task 5 (`SpaceMouseHid` constructor stays inactive).
- Risks 2-4 (decompile findings) → handled defensively in Task 7 with reflection lookup + clear error logs + Task 11 tests 1, 3, 10 validate.
- Risk 5 (unknown PID) → Task 6 `ExtraProductIds` config + Task 5 logs every 3Dconnexion device for diagnostics.
- Risk 6 (Mac → Windows testing) → Task 10 GitHub Actions Windows build pipeline.
- Test approach (unit on Mac + manual on Windows) → Tasks 2-4 for unit, Task 11 for manual.

No placeholders remain. Type names are consistent (`HeldObjectController`, `SpaceMouseState`, `SpaceMouseReportParser`, `ManipulationConfig`, `ButtonAction`, `PluginConfig`, `SpaceMouseHid`, `GrabPatches`, `Plugin`).
