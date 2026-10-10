namespace SpaceMonopoly.WinForms;

/// <summary>
/// Treats quick repeated clicks on any button as one click. A button only clicks when the mouse is
/// pressed and released on it, so swallowing the second press is enough. The timer is shared by
/// all buttons, so the second click of a double-click can't land on a button in the window behind
/// a dialog that the first click closed.
/// </summary>
internal sealed class ButtonDebounce : IMessageFilter
{
    private const int WM_LBUTTONDOWN = 0x0201;
    private const int WM_LBUTTONDBLCLK = 0x0203;
    private static readonly TimeSpan Interval = TimeSpan.FromMilliseconds(500);

    private DateTime _lastPress = DateTime.MinValue;

    public bool PreFilterMessage(ref Message m)
    {
        if (m.Msg is not (WM_LBUTTONDOWN or WM_LBUTTONDBLCLK)) return false;
        if (Control.FromHandle(m.HWnd) is not ButtonBase) return false;

        var now = DateTime.UtcNow;
        if (now - _lastPress < Interval) return true; // ignore this press
        _lastPress = now;
        return false;
    }
}
