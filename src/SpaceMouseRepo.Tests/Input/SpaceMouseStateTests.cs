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
