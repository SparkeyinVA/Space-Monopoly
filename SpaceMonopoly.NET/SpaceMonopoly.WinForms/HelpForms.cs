using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>How to Play (adapted from the original docs.html) and About.</summary>
internal static class HelpForms
{
    /// <summary>
    /// How to Play, with one tab per game type (F10). It opens on the type being played, and each tab shows only
    /// that type's rules, built from the rule constants (<see cref="RulesText"/>).
    /// </summary>
    public static void ShowHowToPlay(IWin32Window owner, GameType current, Currency currency)
    {
        using var form = Ui.Dialog("How to Play Space Monopoly", 680, 600);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MinimizeBox = false;
        var tabs = new TabControl { Dock = DockStyle.Fill };
        foreach (var type in new[] { GameType.Modern, GameType.Classic, GameType.Tournament })
        {
            var page = new TabPage(type.ToString()) { Padding = new Padding(10), BackColor = SystemColors.Window };
            page.Controls.Add(new RichTextBox
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                BorderStyle = BorderStyle.None,
                BackColor = SystemColors.Window,
                Font = new Font("Segoe UI", 10f),
                Text = RulesText.For(type, currency),
            });
            tabs.TabPages.Add(page);
            if (type == current) tabs.SelectedTab = page;
        }
        var ok = Ui.Button("Close");
        ok.DialogResult = DialogResult.OK;
        form.AcceptButton = form.CancelButton = ok;
        form.Controls.Add(tabs);
        form.Controls.Add(Ui.ButtonBar(ok));
        form.ResumeLayout(false);
        form.ShowDialog(owner);
    }

    public static void ShowAbout(IWin32Window owner)
    {
        string version = typeof(HelpForms).Assembly.GetName().Version?.ToString(2) ?? "2.0";
        Dialogs.Show(owner, "About Space Monopoly", Color.MidnightBlue,
            [
                $"Space Monopoly {version} — the freeware game of buying and selling shares.",
                "Original game © 1998-2001 David Vivash (Borland C++ Builder), released under the GNU GPL v2.",
                "Windows 11 edition by Jerame Chamberlain: rebuilt in C# / .NET with computer and Claude opponents.",
            ]);
    }
}
