namespace SpaceMonopoly.WinForms;

/// <summary>
/// A bar above the board for first-time tips and tutorial steps (S12). It stays until dismissed and never stops play.
/// </summary>
internal sealed class CoachBar : Panel
{
    private readonly Label _title = new() { AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold), ForeColor = Color.FromArgb(20, 60, 120) };
    private readonly Label _text = new() { AutoSize = false, Dock = DockStyle.Fill, ForeColor = Color.FromArgb(20, 30, 60), TextAlign = ContentAlignment.MiddleLeft };
    private readonly Button _ok = new() { Text = "Got it", AutoSize = true };
    private readonly Button _skip = new() { Text = "Skip tutorial", AutoSize = true };

    public CoachBar()
    {
        Dock = DockStyle.Top;
        Height = 64;
        Visible = false;
        BackColor = Color.FromArgb(255, 248, 214);
        Padding = new Padding(10, 6, 10, 6);
        Font = new Font("Segoe UI", 9.5f);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Right, AutoSize = true, FlowDirection = FlowDirection.LeftToRight, WrapContents = false };
        buttons.Controls.AddRange([_skip, _ok]);
        var left = new Panel { Dock = DockStyle.Fill };
        _title.Dock = DockStyle.Top;
        left.Controls.Add(_text);
        left.Controls.Add(_title);
        Controls.Add(left);
        Controls.Add(buttons);

        _ok.Click += (_, _) => { Visible = false; Dismissed?.Invoke(this, EventArgs.Empty); };
        _skip.Click += (_, _) => SkipRequested?.Invoke(this, EventArgs.Empty);
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
        _ok.Visible = true;
        _skip.Visible = false;
        Visible = true;
    }

    /// <summary>A tutorial step. Steps that wait for an action have no "Got it" button.</summary>
    public void ShowStep(int number, int count, string text, bool needsAcknowledgement)
    {
        _title.Text = $"Tutorial — step {number} of {count}";
        _text.Text = text;
        _ok.Text = "Next";
        _ok.Visible = needsAcknowledgement;
        _skip.Visible = true;
        Visible = true;
    }

    public void HideBar()
    {
        Visible = false;
        _ok.Text = "Got it";
    }
}
