# SpaceMouse Support for R.E.P.O. — Design

**Date:** 2026-04-25
**Status:** Approved, ready for implementation planning
**Distribution:** Thunderstore mod, installable via r2modman

## Goal

Let players who own a 3Dconnexion SpaceMouse use it to rotate and finely position objects they are holding in R.E.P.O., enabling precise placement (shelving valuables, aligning fragile items) that the stock mouse-and-keyboard grab system can't do well.

## Scope

**In scope:**
- A BepInEx 5 plugin that reads the SpaceMouse via raw HID and feeds 6-DOF input into the held object's target transform.
- Distribution as a Thunderstore mod consumable by r2modman.
- Configurable sensitivities, deadzones, axis inversion, and button bindings via a BepInEx config file.
- Graceful no-op when no SpaceMouse is attached.

**Out of scope (this version):**
- SpaceMouse control of the player camera or movement.
- Any networked state beyond what the vanilla grab system already replicates.
- Linux/macOS runtime support (R.E.P.O. is Windows-only).
- Models other than the SpaceMouse Wireless / Wireless Compact for first release. Other models will work as long as their HID report layout matches the standard 3Dconnexion format; button counts above two are remappable via config.

## Manipulation Model (the UX decision)

Mouse + scroll keep their vanilla roles. SpaceMouse adds object-local control without ever touching the camera:

| Input | Controls |
|-------|----------|
| Mouse aim (vanilla) | Coarse position of held object — where the player points, the object follows |
| Scroll wheel (vanilla) | Push/pull depth of held object |
| SpaceMouse rotation axes (Rx/Ry/Rz) | Full 6-DOF rotation of the held object, accumulated each frame |
| SpaceMouse translation axes (Tx/Ty/Tz) | Small local offset from the aim point, clamped to a configurable radius (default 15 cm), accumulated each frame, zeroed on release |
| SpaceMouse Button 1 | Reset accumulated rotation back to vanilla target (configurable) |
| SpaceMouse Button 2 | Toggle precision mode — 5× gain reduction on all axes (configurable) |

Rationale for the split: the SpaceMouse puck has constant low-amplitude drift, so coupling any axis to camera or movement would make aiming wobbly. Confining its effect to the held object keeps the device from competing with mouse input.

## Architecture

Standard BepInEx 5 plugin, .NET Framework 4.7.2.

```
SpaceMouseRepo/
├── plugin/
│   ├── Plugin.cs                       BepInEx entry, lifecycle, Harmony bootstrap
│   ├── Input/
│   │   ├── SpaceMouseHid.cs            HID device discovery + background read loop
│   │   └── SpaceMouseState.cs          Immutable snapshot: 6 axes [-1,1] + 2 buttons
│   ├── Patches/
│   │   └── GrabPatches.cs              Harmony postfix on PhysGrabber per-frame method
│   ├── Behavior/
│   │   └── HeldObjectController.cs     Applies axes → rotation delta + clamped offset
│   └── Config.cs                       BepInEx ConfigFile bindings
├── icon.png                            Required for Thunderstore
├── manifest.json                       Required for Thunderstore / r2modman
└── README.md
```

**Boundaries:**
- `SpaceMouseHid` knows HID, knows nothing about R.E.P.O.
- `HeldObjectController` knows grab math, knows nothing about HID.
- `GrabPatches` is the only file touching R.E.P.O. internals.
- `Plugin` is the only file that wires them together.

This isolation means the input layer is unit-testable from canned byte arrays, the math layer is unit-testable from synthetic axis values, and only the patch layer requires the running game.

## Component Detail

### SpaceMouseHid (input layer)

- Uses **HidLibrary** (NuGet, MIT-licensed, vendored as a DLL alongside the plugin so r2modman ships it).
- On `Plugin.Awake`: enumerate HID devices, match vendor `0x256F` and a known product ID set (`0xC62E`, `0xC62F`, `0xC652` known; other 3Dconnexion products allowed via configurable extra-IDs list).
- If no match, log a warning and stay dormant for the rest of the session — patches are never applied, plugin is a complete no-op.
- If a match is found, open the device and start a background thread that reads HID reports continuously and parses by report ID:
  - Report 1 → translation: 3 × int16 LE → Tx, Ty, Tz, normalized by ÷350 and clamped to `[-1, 1]`.
  - Report 2 → rotation: 3 × int16 LE → Rx, Ry, Rz, normalized by ÷350 and clamped to `[-1, 1]`.
  - Report 3 → buttons: bitmask, bits 0–1 stored as bools.
- Publishes a `volatile` reference to an immutable `SpaceMouseState` struct after each read. The patch layer reads this on the main thread without locks.
- Applies the configured deadzone before publishing.

### HeldObjectController (math layer)

Config values are converted to engine-native units once at load: `rotSensDegPerSec` stays in degrees per second (Unity's `Quaternion.Euler` takes degrees). `transSensMPerSec = TranslationCmPerSec / 100`. `maxOffsetM = MaxLocalOffsetCm / 100`. The math below uses the engine-native variants.

`Apply(PhysGrabber holder, SpaceMouseState s)`:
1. Compute `dt = Time.deltaTime`.
2. **Rotation accumulator** (per `PhysGrabber` instance, reset on grab/release):
   - `deltaRot = Quaternion.Euler(s.Rx * rotSensDegPerSec * dt, s.Ry * rotSensDegPerSec * dt, s.Rz * rotSensDegPerSec * dt)`
   - `accRot = deltaRot * accRot`
3. **Translation accumulator** (Vector3 in meters, reset on grab/release):
   - `accOffset += new Vector3(s.Tx, s.Ty, s.Tz) * transSensMPerSec * dt`
   - `accOffset = Vector3.ClampMagnitude(accOffset, maxOffsetM)`
4. Apply `accRot` and `accOffset` to the held object's vanilla target transform fields (the exact write target is determined in implementation step 1; see Risks).
5. Precision-mode flag scales `rotSens` and `transSens` by 0.2× while active.
6. Reset-rotation button zeros `accRot` to identity.

### GrabPatches (patch layer)

A single Harmony postfix on `PhysGrabber`'s per-frame update method (exact name TBD — see Risks). Pseudocode:

```csharp
[HarmonyPostfix]
static void PostGrabUpdate(PhysGrabber __instance) {
    if (!__instance.isLocal) return;
    if (__instance.grabbed == null) {
        HeldObjectController.OnRelease(__instance);
        return;
    }
    HeldObjectController.Apply(__instance, SpaceMouseHid.Current.State);
}
```

If the vanilla code computes the target inline rather than storing it in a writable field, this becomes a Harmony transpiler instead of a postfix — flagged as a Risk.

### Config

BepInEx `ConfigFile` at `BepInEx/config/com.zanewebb.spacemouse_repo.cfg`:

```
[Sensitivity]
RotationDegPerSec = 180
TranslationCmPerSec = 30
MaxLocalOffsetCm = 15

[Deadzone]
TranslationDeadzone = 0.05
RotationDeadzone = 0.05

[AxisInversion]
InvertTx, InvertTy, InvertTz, InvertRx, InvertRy, InvertRz = false

[Bindings]
Button1 = ResetRotation
Button2 = TogglePrecisionMode
# Available actions: ResetRotation, TogglePrecisionMode, None
# (Future: DropObject, FreezeObject, etc.)

[Hardware]
ExtraProductIds =          # comma-separated hex IDs, e.g. 0xC631
```

## Multiplayer Behavior

Client-side only. The mod runs entirely on the player using the SpaceMouse. Other players do not need it installed.

The patch only mutates state when `isLocal && grabbed != null`, and writes into the same target-transform field the vanilla code already uses. R.E.P.O.'s existing Photon networking replicates that field to other clients. No new RPCs, no new state, no protocol change → no desync vector.

This is verified at implementation time by playtesting a held object in a 2-player lobby with one client modded and one vanilla, watching the unmodded client's view in OBS or similar.

## Risks and Open Investigation Items

The first step of implementation is investigation, not coding. These must be resolved before patches are written:

1. **R.E.P.O. runtime: Mono vs. IL2CPP.**
   *Why it matters:* Determines whether we use BepInEx 5 (Mono, Harmony-friendly) or BepInEx 6 IL2CPP (different patching API).
   *Resolution:* Inspect `R.E.P.O./BepInEx/` after a baseline r2modman install; check for `Assembly-CSharp.dll` (Mono) vs. `GameAssembly.dll` (IL2CPP). Confirmed assumption: Mono / BepInEx 5, based on R.E.P.O.'s active Thunderstore presence.

2. **Exact patch target on `PhysGrabber`.**
   *Why it matters:* We need the per-frame method that produces the held object's target rotation and position. Wrong target → no effect, or breaks unrelated grab behavior.
   *Resolution:* Decompile `Assembly-CSharp.dll` with dnSpy. Locate `PhysGrabber`. Identify the method that reads input and writes the target transform. Likely candidates: `Update`, `LateUpdate`, `FixedUpdate`, or a named method like `PhysGrabPointUpdate`.

3. **Target rotation/position writability.**
   *Why it matters:* If the target is a public/protected field, a Harmony postfix can mutate it directly. If it's a local variable inside the per-frame method, we need a transpiler (riskier and more brittle to game updates).
   *Resolution:* During step 2, note whether the target is a field on `PhysGrabber` or `PhysGrabObject`, or a local. Choose postfix vs. transpiler accordingly.

4. **Rotation representation.**
   *Why it matters:* Composing a delta differs for quaternion vs. euler vs. axis-angle storage.
   *Resolution:* Same decompilation pass.

5. **HID product ID for the user's specific SpaceMouse Wireless.**
   *Why it matters:* If the device has an unlisted product ID, the plugin won't bind. Mitigated by the `ExtraProductIds` config.
   *Resolution:* On first install, log all 3Dconnexion-vendor HID devices found, regardless of product match, so the user can paste the ID into config if needed.

6. **Windows-only testing from a macOS development host.**
   *Why it matters:* The author is on macOS; R.E.P.O. is Windows-only.
   *Resolution:* Build via GitHub Actions (Windows runner) or local `dotnet build` (Mac can produce the .NET Framework 4.7.2 DLL with the right targeting pack). Runtime testing requires a Windows PC with the SpaceMouse attached. Flagged as a workflow constraint, not a design constraint.

## Testing Approach

**Unit tests** (xUnit, runnable on Mac):
- `SpaceMouseHid` report parsing: feed canned `byte[]` reports → assert expected `SpaceMouseState`.
- `HeldObjectController` math: synthetic axis inputs and dt → assert expected rotation accumulator and clamped offset, including reset-on-release behavior, precision-mode scaling, deadzone, axis inversion.

**Manual smoke tests** (Windows host, in-game):
1. Plugin loads with no device attached → log shows graceful no-op, game plays normally.
2. Plugin loads with device attached → log shows device discovery success.
3. Pick up an object → puck rotation rotates it; release → object keeps last rotation as the vanilla physics takes over.
4. Hold object near a shelf → puck translation nudges it sideways within clamp radius.
5. Press Button 1 → rotation snaps back to vanilla target.
6. Press Button 2 → fine-control gain kicks in.
7. 2-player lobby (one modded, one vanilla) → unmodded client sees the same manipulations as the modded client.

## Assumptions

- R.E.P.O. uses Mono + BepInEx 5 + HarmonyX. Confirmed in implementation step 1.
- The held-object target transform is reachable from a Harmony postfix without a transpiler. Fallback: transpiler. Confirmed in implementation step 1.
- `HidLibrary` works under BepInEx 5's loader. If not, fall back to `HidSharp` or a thin P/Invoke wrapper around `hid.dll`.

## Out-of-Scope, Possibly-Future Work

- More button actions (drop, freeze, scale, ghost-preview placement).
- Per-object sensitivity profiles (e.g. larger objects rotate slower).
- An in-game overlay showing current rotation gizmo.
- Support for the official 3DxWare SDK as an alternate input backend (richer but adds a runtime dependency on the proprietary driver).
