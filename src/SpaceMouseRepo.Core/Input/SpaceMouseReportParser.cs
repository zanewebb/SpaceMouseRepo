using System;

namespace SpaceMouseRepo.Core.Input;

public sealed class SpaceMouseReportParser
{
    private const float AxisDivisor = 350f;

    private readonly float _translationDeadzone;
    private readonly float _rotationDeadzone;

    // volatile: HID read thread writes via Feed(); main thread reads via State.
    // Each individual axis read/write is atomic (float/bool); volatile prevents
    // the JIT from caching a stale value in a register across the read.
    private volatile float _tx, _ty, _tz, _rx, _ry, _rz;
    private volatile bool _b1, _b2;

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
