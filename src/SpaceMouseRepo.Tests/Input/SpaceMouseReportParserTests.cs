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

    private static byte[] CombinedReport(short tx, short ty, short tz, short rx, short ry, short rz) =>
        new byte[]
        {
            0x01,
            (byte)(tx & 0xFF), (byte)((tx >> 8) & 0xFF),
            (byte)(ty & 0xFF), (byte)((ty >> 8) & 0xFF),
            (byte)(tz & 0xFF), (byte)((tz >> 8) & 0xFF),
            (byte)(rx & 0xFF), (byte)((rx >> 8) & 0xFF),
            (byte)(ry & 0xFF), (byte)((ry >> 8) & 0xFF),
            (byte)(rz & 0xFF), (byte)((rz >> 8) & 0xFF),
        };

    [Fact]
    public void Combined_13_byte_report_populates_all_six_axes_in_one_pass()
    {
        var p = new SpaceMouseReportParser();
        p.Feed(CombinedReport(350, -350, 175, -175, 350, -350));
        Assert.Equal(1.0f,  p.State.Tx, 3);
        Assert.Equal(-1.0f, p.State.Ty, 3);
        Assert.Equal(0.5f,  p.State.Tz, 3);
        Assert.Equal(-0.5f, p.State.Rx, 3);
        Assert.Equal(1.0f,  p.State.Ry, 3);
        Assert.Equal(-1.0f, p.State.Rz, 3);
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
