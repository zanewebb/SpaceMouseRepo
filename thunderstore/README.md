# SpaceMouseRepo

Adds 3Dconnexion SpaceMouse support to R.E.P.O. While holding an object, the SpaceMouse puck rotates it in 6DOF and applies a small clamped local offset. Mouse aim and scroll-wheel depth keep their vanilla roles. The camera is never touched.

## ⚠️ FIRST THING TO DO AFTER INSTALL: TUNE THE DEADZONE

Every SpaceMouse has a different amount of idle "drift" — sensor noise the puck emits while sitting still. Out of the box this mod uses a **20% deadzone**, which works for some units but is too low for many. **If your held object jitters back and forth while you're not touching the puck, the deadzone is too low.**

**To tune (no game restart needed):**
1. Open r2modman → click the gear icon next to `SpaceMouseRepo` → Configuration
2. Bump **`Deadzone → Translation`** and **`Deadzone → Rotation`** sliders up by 5% at a time
3. Test in-game until the held object sits still when you're not touching the puck

Typical values:
- **Brand-new SpaceMouse:** 5–10%
- **Average / a few years old:** 15–25%
- **Older or worn unit:** 25–40%

If you can't find a deadzone that stops the jitter, your puck has too much drift for this mod's strategy alone — let me know via GitHub Issues and we'll explore filtering options.

## Requirements

- A 3Dconnexion SpaceMouse (Wireless, Wireless Compact, Pro, Enterprise — anything that the 3DxWare driver recognizes)
- The official **3Dconnexion 3DxWare** driver installed (the SDK siappdll is what this mod talks to)
- BepInExPack 5.4.21+

## Multiplayer

Client-side only. Only the player using the SpaceMouse needs the mod installed; other clients receive normal networked transform updates.

## Other configurable settings

All available in the same r2modman config panel:

- **Sensitivity → RotationDegPerSec** — rotation speed at full puck deflection (default 180°/s)
- **Sensitivity → TranslationCmPerSec** — translation speed at full puck deflection (default 30 cm/s)
- **Sensitivity → MaxLocalOffsetCm** — radius limit on accumulated local offset (default 15 cm)
- **Sensitivity → PrecisionScale** — gain reduction when precision mode is active (default 0.2 = 5× finer)
- **AxisInversion → Invert\<axis\>** — flip individual axis directions if needed
- **Bindings → Button1 / Button2** — `ResetRotation`, `TogglePrecisionMode`, or `None`

All edits apply live without restarting the game.

## Troubleshooting

- **Held object doesn't move at all** — make sure the 3Dconnexion driver is installed and running. If you kill `3DxWareUI.exe` from Task Manager, all SpaceMouse input dies, and that's how you can tell whether the driver is supplying our input or not.
- **Held object jitters constantly** — see the deadzone section above.
- **One axis controls the wrong direction** — flip the matching `Invert*` toggle in config.
- **Want detailed diagnostics?** A side-channel log is written to `<r2modman profile>/LogOutput.SpaceMouseRepo.log` with heartbeat telemetry every 5 seconds.

## Source / issues

https://github.com/zanewebb/SpaceMouseRepo
