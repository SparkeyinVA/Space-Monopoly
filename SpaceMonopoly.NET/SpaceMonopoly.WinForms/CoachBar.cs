namespace SpaceMonopoly.WinForms;

/// <summary>
/// A bar above the board for first-time tips and tutorial steps (S12). It stays until dismissed and never stops play.
/// Its height follows its text at the current scaling (F19), so nothing is cut off.
/// </summary>
internal sealed class CoachBar : Panel
{
    private readonly Label _title = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(20, 60, 120) };
    private readonly Label _text = new() { AutoSize = false, ForeColor = Color.FromArgb(20, 30, 60) };
    private readonly Label _doThis = new() { AutoSize = false, Font = new Font("Segoe UI", 9.5f, FontStyle.Bold), ForeColor = Color.FromArgb(150, 60, 0) };
    private readonly Label _waiting = new() { AutoSize = false, ForeColor = Color.DimGray, TextAlign = ContentAlignment.MiddleCenter };

    // Fixed logical sizes, as in Ui.Button: auto-sized buttons end up scaled twice on high-DPI screens.
    private readonly Button _ok = Ui.Button("Got it", width: 90);
    private readonly Button _skip = Ui.Button("Skip tutorial", width: 110);

    public CoachBar()
    {
        Dock = DockStyle.Top;
        Height = 64;
        Visible = false;
        BackColor = Color.FromArgb(255, 248, 214);
        Padding = new Padding(10, 6, 10, 6);
        Font = new Font("Segoe UI", 9.5f);
        Controls.AddRange([_title, _text, _doThis, _waiting, _ok, _skip]);

        _ok.Click += (_, _) => { Visible = false; Dismissed?.Invoke(this, EventArgs.Empty); };
        _skip.Click += (_, _) => SkipRequested?.Invoke(this, EventArgs.Empty);
        Resize += (_, _) => Arrange();
    }

    /// <summary>The player closed the tip or acknowledged the step.</summary>
    public event EventHandler? Dismissed;

    /// <summary>The player asked to leave the tutorial.</summary>
    public event EventHandler? SkipRequested;

    /// <summary>True while a tip or step is showing.</summary>
    public bool Showing => Visible;

    public void ShowTip(string text)
    {
        _title.Text = "Tip";
        _text.Text = text;
        _doThis.Text = "";
        _waiting.Text = "";
        _ok.Text = "Got it";
        _ok.Visible = true;
        _skip.Visible = false;
        Visible = true;
        Arrange();
    }

    /// <summary>
    /// A tutorial step. A step that waits for an action shows what to do in bold and, where Next would be, what it's
    /// waiting for; a step that only explains something has a Next button.
    /// </summary>
    public void ShowStep(int number, int count, string text, bool needsAcknowledgement, string? doThis = null, string? waiting = null)
    {
        _title.Text = $"Tutorial — step {number} of {count}";
        _text.Text = text;
        _doThis.Text = doThis is null ? "" : "▶ Do this: " + doThis;
        _waiting.Text = needsAcknowledgement ? "" : waiting ?? "Waiting for you";
        _ok.Text = "Next";
        _ok.Visible = needsAcknowledgement;
        _skip.Visible = true;
        Visible = true;
        Arrange();
    }

    public void HideBar()
    {
        Visible = false;
        _ok.Text = "Got it";
    }

    /// <summary>Lays out the text beside the buttons and sets the bar's height to fit it, up to a third of the window.</summary>
    private void Arrange()
    {
        if (!Visible || ClientSize.Width <= 0) return;
        int gap = LogicalToDeviceUnits(8);
        int buttonsWidth = LogicalToDeviceUnits(130);
        int left = Padding.Left, top = Padding.Top;
        int textWidth = Math.Max(LogicalToDeviceUnits(200), ClientSize.Width - Padding.Horizontal - buttonsWidth - gap);

        _title.Location = new Point(left, top);
        int y = top + _title.Height + LogicalToDeviceUnits(2);
        y = Place(_text, left, y, textWidth);
        if (_doThis.Text.Length > 0) y = Place(_doThis, left, y + LogicalToDeviceUnits(4), textWidth);
        _doThis.Visible = _doThis.Text.Length > 0;

        // Buttons (and the waiting label) on the right, one under the other.
        int bx = ClientSize.Width - Padding.Right - buttonsWidth;
        int by = top;
        foreach (var b in new[] { _skip, _ok })
        {
            if (!b.Visible) continue;
            b.Location = new Point(bx + (buttonsWidth - b.Width) / 2, by);
            by += b.Height + LogicalToDeviceUnits(4);
        }
        if (_waiting.Text.Length > 0)
        {
            var size = TextRenderer.MeasureText(_waiting.Text, _waiting.Font, new Size(buttonsWidth, int.MaxValue), TextFormatFlags.WordBreak);
            _waiting.Bounds = new Rectangle(bx, by, buttonsWidth, size.Height);
            by += size.Height;
        }
        _waiting.Visible = _waiting.Text.Length > 0;

        int wanted = Math.Max(y, by) + Padding.Bottom;
        int cap = Parent is { } p ? Math.Max(LogicalToDeviceUnits(120), p.ClientSize.Height / 3) : int.MaxValue;
        wanted = Math.Min(wanted, cap);
        if (Height != wanted) Height = wanted;
    }

    /// <summary>Places a word-wrapped label at the given width and returns the y below it.</summary>
    private static int Place(Label label, int x, int y, int width)
    {
        var size = TextRenderer.MeasureText(label.Text, label.Font, new Size(width, int.MaxValue), TextFormatFlags.WordBreak);
        label.Bounds = new Rectangle(x, y, width, size.Height);
        return y + size.Height;
    }
}
