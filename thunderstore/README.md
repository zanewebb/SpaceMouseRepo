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
