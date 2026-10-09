using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>
/// The pop-ups for new companies, mergers, splits and the end of the game
/// (ComForm, Merger, SplitForm and OverForm in the original).
/// </summary>
internal static class Dialogs
{
    public static void ShowEvent(IWin32Window owner, Game game, GameEvent e, Currency currency)
    {
        string Name(int p) => game.Players[p].Name;
        string Co(int c) => Company.Names[c];

        switch (e)
        {
            case CompanyFoundedEvent f:
                Show(owner, "New Company", Color.FromArgb(Company.Colors[f.Company]),
                    [
                        // Some names already end in a full stop ("Atlantis Ltd.").
                        $"The new company's name is {Co(f.Company)}" + (Co(f.Company).EndsWith('.') ? "" : "."),
                        $"The shares will start at {currency.Format(f.SharePrice)} each.",
                        $"{Name(f.Player)} will get {f.FreeShares} free shares in the company and {currency.Format(f.Bonus)}.",
                    ]);
                break;

            case MergerEvent m:
            {
                var table = Ui.Table(("Player", 140), ($"Shares in {Co(m.Survivor)}", 140), ($"Shares in {Co(m.Absorbed)}", 140), ("Money earned", 120));
                foreach (var r in m.Rows)
                    table.Items.Add(Ui.Row(Name(r.Player), r.SurvivorShares.ToString("N0"), r.AbsorbedShares.ToString("N0"), currency.Format(r.Profit)));
                Show(owner, "Merger", Color.FromArgb(Company.Colors[m.Survivor]),
                    [
                        $"There has been a merger between {Co(m.Survivor)} and {Co(m.Absorbed)}.",
                        $"The new company will take the name of {Co(m.Survivor)}" + (Co(m.Survivor).EndsWith('.') ? "" : "."),
                        $"The cost of {Co(m.Survivor)} shares will be {currency.Format(m.NewValue)}.",
                    ], table);
                break;
            }

            case SplitEvent s:
            {
                var table = Ui.Table(("Player", 160), ("Shares before", 140), ("Shares after", 140));
                foreach (var r in s.Rows)
                    table.Items.Add(Ui.Row(Name(r.Player), r.Before.ToString("N0"), r.After.ToString("N0")));
                Show(owner, "Share Split", Color.FromArgb(Company.Colors[s.Company]),
                    [
                        $"Shares in {Co(s.Company)} have split two for one.",
                        $"The previous share price of {currency.Format(s.OldValue)} has been changed to {currency.Format(s.NewValue)}.",
                    ], table);
                break;
            }
        }
    }

    public static void ShowGameOver(IWin32Window owner, Game game, Currency currency)
    {
        var standings = game.Standings();
        var winner = standings[0];

        var lines = new List<string>
        {
            $"At go number {game.Turn} the game is finally over. Here are the final standings.",
            $"The winner is {winner.Name} with {currency.Format(winner.NetWorth)}!",
        };

        // Biggest company on the winner's board (the only board in a classic game).
        var world = game.WorldFor(winner.Player);
        var biggest = Enumerable.Range(0, Company.Count)
            .Select(c => (Company: c, Size: world.Board.CompanySize(c)))
            .MaxBy(t => t.Size);
        if (biggest.Size > 0)
            lines.Add($"Biggest company: {Company.Names[biggest.Company]} with {biggest.Size} squares.");

        var table = Ui.Table(("Player", 150), ("Type", 140), ("Money left", 110), ("Worth of shares", 120), ("Net wealth", 120));
        foreach (var s in standings)
            table.Items.Add(Ui.Row(s.Name, s.Kind.Label(), currency.Format(s.Money), currency.Format(s.ShareWorth), currency.Format(s.NetWorth)));

        Show(owner, "Game Over", Color.Gold, lines, table);
    }

    public static void Show(IWin32Window owner, string title, Color accent, IReadOnlyList<string> lines, ListView? table = null)
    {
        using var form = Ui.Dialog(title, table is null ? 460 : 660, table is null ? 210 : 400);

        var strip = new Panel { Dock = DockStyle.Top, Height = 10, BackColor = accent };
        var text = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 60,
            Padding = new Padding(14, 10, 14, 4),
            Text = string.Join(Environment.NewLine + Environment.NewLine, lines),
            Font = new Font("Segoe UI", 10f),
        };

        var ok = Ui.Button("OK");
        ok.DialogResult = DialogResult.OK;
        form.AcceptButton = ok;
        form.CancelButton = ok;

        if (table is not null)
        {
            var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12, 4, 12, 4) };
            holder.Controls.Add(table);
            form.Controls.Add(holder);
        }
        form.Controls.Add(text);
        form.Controls.Add(strip);
        var bar = Ui.ButtonBar(ok);
        form.Controls.Add(bar);
        form.ResumeLayout(false);

        // Sizes are now scaled for the screen's DPI, so measure the wrapped text in real pixels.
        var room = new Size(form.ClientSize.Width - text.Padding.Horizontal, 0);
        text.Height = TextRenderer.MeasureText(text.Text, text.Font, room, TextFormatFlags.WordBreak).Height + text.Padding.Vertical + 8;
        if (table is null)
            form.ClientSize = new Size(form.ClientSize.Width, strip.Height + text.Height + bar.PreferredSize.Height);

        form.ShowDialog(owner);
    }
}
