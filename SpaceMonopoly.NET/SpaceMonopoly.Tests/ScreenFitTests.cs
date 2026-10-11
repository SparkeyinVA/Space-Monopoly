using SpaceMonopoly.Core;
using static SpaceMonopoly.Core.ScreenFit;

namespace SpaceMonopoly.Tests;

/// <summary>Keeping windows on screen (F17).</summary>
public class ScreenFitTests
{
    // The user's set-up: the main screen's working area, and a second monitor to the left with negative coordinates.
    private static readonly Box Main = new(0, 0, 3840, 2280);
    private static readonly Box Left = new(-6400, 0, 6400, 3600);

    [Fact]
    public void A_dialog_above_the_top_of_a_left_hand_monitor_is_moved_down_so_its_title_bar_shows()
    {
        // What the user saw: the New Game window half above the top of the second monitor.
        var fitted = Clamp(new Box(-3500, -400, 1900, 1400), Left);
        Assert.Equal(0, fitted.Top);
        Assert.Equal(-3500, fitted.Left);
        Assert.Equal((1900, 1400), (fitted.Width, fitted.Height));
    }

    [Fact]
    public void A_window_past_any_edge_comes_back_inside()
    {
        Assert.Equal(new Box(-6400, 0, 800, 600), Clamp(new Box(-7000, -50, 800, 600), Left));
        Assert.Equal(new Box(-800, 3000, 800, 600), Clamp(new Box(-300, 3500, 800, 600), Left));
        Assert.Equal(new Box(3040, 1680, 800, 600), Clamp(new Box(3500, 2000, 800, 600), Main));
    }

    [Fact]
    public void A_window_larger_than_the_screen_is_shrunk_to_fit()
    {
        var fitted = Clamp(new Box(-100, -100, 5000, 3000), Main);
        Assert.Equal(Main, fitted);
    }

    [Fact]
    public void A_dialog_is_centred_on_its_owner_but_never_off_screen()
    {
        // The main window as measured on the second monitor, with a tall dialog centred on it.
        var owner = new Box(-3635, 1063, 2783, 2280);
        var centred = CentreOn(owner, 1000, 600, Left);
        Assert.Equal(owner.Left + (owner.Width - 1000) / 2, centred.Left);
        Assert.Equal(owner.Top + (owner.Height - 600) / 2, centred.Top);

        var nearTop = owner with { Top = 100 };
        var tall = CentreOn(nearTop, 1000, 3400, Left);
        Assert.Equal(0, tall.Top);                    // centring would have put it 460 pixels above the screen
        Assert.True(tall.Bottom <= Left.Bottom);
    }
}
