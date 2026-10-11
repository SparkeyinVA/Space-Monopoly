using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>
/// The high score tables for all three grid sizes, for the 2001 rules and for Modern games.
/// New entries are marked like the original's red blocks.
/// </summary>
internal sealed class HighScoresForm : Form
{
    public HighScoresForm(HighScoreTable table, Currency currency, GridSize show, bool showModern,
        IReadOnlyCollection<(GridSize Size, bool Modern, int Index)> fresh)
    {
        SuspendLayout();
        Text = "High Scores";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(700, 380);

        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (bool modern in new[] { true, false })
        foreach (var size in Enum.GetValues<GridSize>())
        {
            var page = new TabPage((modern ? "Modern · " : "") + size) { Padding = new Padding(6) };
            var list = Ui.Table(("#", 44), ("Name", 170), ("Net wealth", 130), ("Player type", 150), ("Game", 80), ("Date", 80));
            var entries = table.For(size, modern);
            for (int i = 0; i < entries.Count; i++)
            {
                var e = entries[i];
                var row = Ui.Row((i + 1).ToString(), e.Name, currency.Format(e.Wealth), e.Kind.Label(), e.GameType.ToString(), e.Date.ToShortDateString());
                if (fresh.Contains((size, modern, i)))
                {
                    // Colour and a star only: a separate bold font for the row isn't scaled with the
                    // rest of the list on high-DPI screens, so it came out too big and was cut off.
                    row.BackColor = Color.MistyRose;
                    row.ForeColor = Color.DarkRed;
                    row.Text = "★ " + row.Text;
                }
                list.Items.Add(row);
            }
            page.Controls.Add(list);
            tabs.TabPages.Add(page);
            if (size == show && modern == showModern) tabs.SelectedTab = page;
        }

        var ok = Ui.Button("OK");
        ok.DialogResult = DialogResult.OK;
        AcceptButton = CancelButton = ok;
        Controls.Add(tabs);
        Controls.Add(Ui.ButtonBar(ok));
        ResumeLayout(false);
        PerformLayout();
    }
}
