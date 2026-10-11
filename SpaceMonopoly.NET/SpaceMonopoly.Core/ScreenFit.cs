namespace SpaceMonopoly.Core;

/// <summary>
/// Keeps a window inside a screen's working area (F17): never larger than the area, and moved so that it, including the
/// whole title bar, is inside. Plain numbers so it can be tested; screens left of or above the main one have negative
/// coordinates.
/// </summary>
public static class ScreenFit
{
    public readonly record struct Box(int Left, int Top, int Width, int Height)
    {
        public int Right => Left + Width;
        public int Bottom => Top + Height;
    }

    /// <summary>The window's new bounds: shrunk to fit the area if needed, then moved fully inside it.</summary>
    public static Box Clamp(Box window, Box area)
    {
        int width = Math.Min(window.Width, area.Width);
        int height = Math.Min(window.Height, area.Height);
        int left = Math.Clamp(window.Left, area.Left, area.Right - width);
        int top = Math.Clamp(window.Top, area.Top, area.Bottom - height);
        return new Box(left, top, width, height);
    }

    /// <summary>A window of the given size centred on another (its owner), then kept inside the area.</summary>
    public static Box CentreOn(Box owner, int width, int height, Box area) =>
        Clamp(new Box(owner.Left + (owner.Width - width) / 2, owner.Top + (owner.Height - height) / 2, width, height), area);
}
