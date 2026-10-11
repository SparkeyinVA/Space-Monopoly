using System.Drawing.Drawing2D;
using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>
/// The end-of-game screen (S10): the winner, a net-worth chart per player over the rounds, the final standings,
/// awards, and any achievements or daily best just earned.
/// </summary>
internal sealed class GameOverForm : Form
{
    private static readonly Color[] LineColors =
        [Color.Gold, Color.DeepSkyBlue, Color.OrangeRed, Color.LimeGreen, Color.Violet, Color.White, Color.Orange, Color.Cyan];

    private readonly Game _game;
    private readonly Currency _currency;

    public GameOverForm(Game game, Currency currency, IReadOnlyList<Award> awards, IReadOnlyList<string> extras)
    {
        _game = game;
        _currency = currency;
        var standings = game.Standings();

        SuspendLayout();
        Text = "Game Over";
        StartPosition = FormStartPosition.CenterParent;
        Ui.KeepOnScreen(this);   // F17: never off the top of a second monitor
        ShowInTaskbar = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(760, 640);
        MinimumSize = new Size(600, 480);

        var title = Ui.Label($"{standings[0].Name} wins with {currency.Format(standings[0].NetWorth)} after {game.Turn} round{(game.Turn == 1 ? "" : "s")}!", 13f, FontStyle.Bold);
        title.Dock = DockStyle.Top;
        title.Padding = new Padding(12, 10, 12, 6);

        var chart = new Panel { Dock = DockStyle.Top, Height = 230, BackColor = Color.FromArgb(10, 14, 36) };
        chart.Paint += (_, e) => DrawChart(e.Graphics, chart.ClientRectangle);
        chart.Resize += (_, _) => chart.Invalidate();

        var table = game.IsModern
            ? Ui.Table(("Player", 140), ("Secret goal", 230), ("Goal reward", 90), ("Net wealth", 120))
            : Ui.Table(("Player", 160), ("Type", 150), ("Money left", 120), ("Net wealth", 130));
        var goals = game.GameEndEvents.OfType<SecretGoalEvent>().ToDictionary(g => g.Player);
        foreach (var s in standings)
            table.Items.Add(game.IsModern && goals.TryGetValue(s.Player, out var g)
                ? Ui.Row(s.Name, SecretGoals.Description(g.Goal), g.Reward > 0 ? currency.Format(g.Reward) : "not met", currency.Format(s.NetWorth))
                : Ui.Row(s.Name, s.Kind.Label(), currency.Format(s.Money), currency.Format(s.NetWorth)));
        table.Dock = DockStyle.Top;
        table.Height = 30 + standings.Count * 22;   // logical pixels: the form scales it for the screen

        var notes = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Control,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Segoe UI", 9.5f),
            Text = string.Join(Environment.NewLine, extras.Concat(awards.Select(a => $"🏆 {a.Title}: {a.Text}"))),
        };
        var notesHolder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 8, 12, 4) };
        notesHolder.Controls.Add(notes);

        var ok = Ui.Button("OK");
        ok.DialogResult = DialogResult.OK;
        AcceptButton = CancelButton = ok;
        notes.TabStop = false;
        Shown += (_, _) =>   // so the awards text isn't selected when the window opens
        {
            ok.Focus();
            notes.SelectionLength = 0;
        };

        Controls.Add(notesHolder);
        Controls.Add(table);
        Controls.Add(chart);
        Controls.Add(title);
        Controls.Add(Ui.ButtonBar(ok));
        ResumeLayout(false);
        PerformLayout();
    }

    private void DrawChart(Graphics g, Rectangle area)
    {
        var history = _game.NetWorthHistory;
        if (history.Count < 2) return;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        int pad = LogicalToDeviceUnits(36);
        var plot = Rectangle.FromLTRB(area.Left + pad + LogicalToDeviceUnits(30), area.Top + LogicalToDeviceUnits(12), area.Right - pad, area.Bottom - pad);
        double max = Math.Max(1, history.SelectMany(h => h).Max(v => (double)v));

        using var axis = new Pen(Color.FromArgb(90, 255, 255, 255));
        using var text = new SolidBrush(Color.FromArgb(200, 255, 255, 255));
        using var font = new Font("Segoe UI", 8f);
        g.DrawLine(axis, plot.Left, plot.Bottom, plot.Right, plot.Bottom);
        g.DrawLine(axis, plot.Left, plot.Top, plot.Left, plot.Bottom);
        g.DrawString(_currency.Format(new BigInteger(max)), font, text, area.Left + 4, plot.Top - 6);
        g.DrawString(_currency.Format(0), font, text, area.Left + 4, plot.Bottom - 8);
        g.DrawString("Rounds →", font, text, plot.Right - LogicalToDeviceUnits(60), plot.Bottom + 4);

        PointF At(int round, BigInteger worth) =>
            new(plot.Left + (float)round / (history.Count - 1) * plot.Width, plot.Bottom - (float)((double)worth / max * plot.Height));

        for (int p = 0; p < _game.Players.Count; p++)
        {
            var color = LineColors[p % LineColors.Length];
            using var pen = new Pen(color, LogicalToDeviceUnits(2));
            var points = history.Select((h, r) => At(r, h[p])).ToArray();
            g.DrawLines(pen, points);
            using var brush = new SolidBrush(color);
            var end = points[^1];
            g.DrawString(_game.Players[p].Name, font, brush, Math.Min(end.X + 4, area.Right - LogicalToDeviceUnits(80)), end.Y - 7);
        }
    }
}
