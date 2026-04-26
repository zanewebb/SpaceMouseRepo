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
