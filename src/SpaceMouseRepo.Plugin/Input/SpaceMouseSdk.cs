using System;
using System.Reflection;
using BepInEx.Logging;
using SpaceMouseRepo.Core.Input;

namespace SpaceMouseRepo.Input;

/// <summary>
/// Reads SpaceMouse motion via the 3Dconnexion 3DxWare driver's TDxInput COM API.
/// The driver claims exclusive HID access on Windows, so raw HID reads are dead in the
/// water — this is the only stable input path. Late-bound COM via reflection so we don't
/// require the Interop assembly at compile time and can no-op cleanly when the driver
/// isn't installed.
/// </summary>
public sealed class SpaceMouseSdk : IDisposable
{
    private readonly ManualLogSource _log;
    private readonly float _translationDeadzone;
    private readonly float _rotationDeadzone;
    private readonly object? _device;
    private readonly object? _sensor;
    private readonly bool _connected;

    public SpaceMouseSdk(ManualLogSource log, float translationDeadzone, float rotationDeadzone)
    {
        _log = log;
        _translationDeadzone = translationDeadzone;
        _rotationDeadzone = rotationDeadzone;

        Type? deviceType;
        try
        {
            deviceType = Type.GetTypeFromProgID("TDxInput.Device");
        }
        catch (Exception e)
        {
            _log.LogWarning($"TDxInput.Device ProgID lookup threw: {e.Message}. SpaceMouse input inactive.");
            return;
        }

        if (deviceType == null)
        {
            _log.LogWarning("TDxInput.Device ProgID not registered. Install 3Dconnexion 3DxWare to enable SpaceMouse input.");
            return;
        }

        try
        {
            _device = Activator.CreateInstance(deviceType);
            // LoadPreferences is best-effort — the app profile may not exist yet.
            try { Invoke(_device, "LoadPreferences", "REPO"); } catch { /* ignore */ }
            Invoke(_device, "Connect");
            _sensor = Get(_device, "Sensor");
            _connected = _sensor != null;
            if (_connected) _log.LogInfo("3DxWare TDxInput.Device connected; SpaceMouse input active.");
            else _log.LogWarning("3DxWare device created but Sensor was null. Input inactive.");
        }
        catch (Exception e)
        {
            _log.LogError($"3DxWare connect failed: {e}");
        }
    }

    public bool IsActive => _connected;

    public SpaceMouseState State
    {
        get
        {
            if (_sensor == null) return SpaceMouseState.Empty;
            try
            {
                var translation = Get(_sensor, "Translation");
                var rotation = Get(_sensor, "Rotation");
                if (translation == null || rotation == null) return SpaceMouseState.Empty;

                float tx = ToFloat(Get(translation, "X"));
                float ty = ToFloat(Get(translation, "Y"));
                float tz = ToFloat(Get(translation, "Z"));

                // Rotation is an AngleAxis: unit-vector axis (X,Y,Z) plus an Angle.
                // Per-axis angular value = axis_component * angle.
                float ax = ToFloat(Get(rotation, "X"));
                float ay = ToFloat(Get(rotation, "Y"));
                float az = ToFloat(Get(rotation, "Z"));
                float angle = ToFloat(Get(rotation, "Angle"));

                return new SpaceMouseState(
                    Dz(tx, _translationDeadzone),
                    Dz(ty, _translationDeadzone),
                    Dz(tz, _translationDeadzone),
                    Dz(ax * angle, _rotationDeadzone),
                    Dz(ay * angle, _rotationDeadzone),
                    Dz(az * angle, _rotationDeadzone),
                    button1: false, button2: false);
            }
            catch
            {
                return SpaceMouseState.Empty;
            }
        }
    }

    public void Dispose()
    {
        if (!_connected || _device == null) return;
        try { Invoke(_device, "Disconnect"); } catch { /* ignore on shutdown */ }
    }

    private static object? Get(object target, string name) =>
        target.GetType().InvokeMember(name,
            BindingFlags.GetProperty | BindingFlags.Instance | BindingFlags.Public,
            null, target, null);

    private static object? Invoke(object target, string name, params object[] args) =>
        target.GetType().InvokeMember(name,
            BindingFlags.InvokeMethod | BindingFlags.Instance | BindingFlags.Public,
            null, target, args);

    private static float ToFloat(object? v) => v == null ? 0f : Convert.ToSingle(v);

    private static float Dz(float v, float deadzone) => Math.Abs(v) <= deadzone ? 0f : v;
}
