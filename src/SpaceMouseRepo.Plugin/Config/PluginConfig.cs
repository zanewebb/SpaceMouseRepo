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

        _tDead      = cf.Bind("Deadzone",    "Translation",        0.20f, "Translation axis deadzone (0-1, fraction of full deflection). Bump higher if held objects jitter when the puck is at rest.");
        _rDead      = cf.Bind("Deadzone",    "Rotation",           0.20f, "Rotation axis deadzone. Bump higher if held objects jitter when the puck is at rest.");

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
