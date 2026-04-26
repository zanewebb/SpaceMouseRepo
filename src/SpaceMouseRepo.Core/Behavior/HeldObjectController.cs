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
