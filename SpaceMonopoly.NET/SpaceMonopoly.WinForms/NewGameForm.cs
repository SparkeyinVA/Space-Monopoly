using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.WinForms;

/// <summary>Set up players (human or computer) and the game type (Players.cpp in the original).</summary>
internal sealed class NewGameForm : Form
{
    private const int MaxPlayers = 8;

    private static readonly PlayerKind[] Kinds =
        [PlayerKind.Human, PlayerKind.ComputerEasy, PlayerKind.ComputerMedium, PlayerKind.ComputerHard, PlayerKind.Claude];

    private readonly TextBox _name = new() { MaxLength = 15, Width = 150 };
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ListView _players = Ui.Table(("Name", 140), ("Player type", 170));
    private readonly RadioButton _classic = new() { Text = "Classic — everyone shares one board (recommended)", AutoSize = true, Checked = true };
    private readonly RadioButton _tournament = new() { Text = "Tournament — everyone gets their own copy of the same board", AutoSize = true };
    private readonly Button _add, _remove, _up, _down, _go;
    private readonly ClaudeCodeSettings _claudeSettings;

    public NewGameForm(ClaudeCodeSettings claudeSettings, IReadOnlyList<(string Name, PlayerKind Kind)> previous)
    {
        _claudeSettings = claudeSettings;
        SuspendLayout();
        Text = "New Game";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(620, 470);

        foreach (var k in Kinds) _kind.Items.Add(k.Label());
        _kind.SelectedIndex = 0;

        _add = Ui.Button("Add", (_, _) => AddPlayer(), 80);
        _remove = Ui.Button("Remove", (_, _) => RemovePlayer());
        _up = Ui.Button("Move up", (_, _) => MovePlayer(-1));
        _down = Ui.Button("Move down", (_, _) => MovePlayer(+1));
        _go = Ui.Button("Go!", (_, _) => Start());
        var cancel = Ui.Button("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        CancelButton = cancel;
        AcceptButton = _add;

        var addRow = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(8, 8, 8, 0), WrapContents = false };
        addRow.Controls.AddRange([Ui.Label("Name:"), _name, Ui.Label("Plays as:"), _kind, _add]);
        foreach (Control c in addRow.Controls) c.Anchor = AnchorStyles.Left;

        var help = Ui.Label("Add players in the order they will play. Computer players can be mixed with humans; " +
                            "Claude players think using your Claude Code login.");
        help.MaximumSize = new Size(590, 0);
        help.Dock = DockStyle.Top;
        help.Padding = new Padding(8, 4, 8, 4);

        var side = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(4) };
        side.Controls.AddRange([_up, _down, _remove]);

        var listHolder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 0, 4) };
        listHolder.Controls.Add(_players);

        var typeBox = new GroupBox { Text = "Game type", Dock = DockStyle.Bottom, Height = 80, Padding = new Padding(10, 6, 10, 6) };
        var typeFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown };
        typeFlow.Controls.AddRange([_classic, _tournament]);
        typeBox.Controls.Add(typeFlow);

        Controls.Add(listHolder);
        Controls.Add(side);
        Controls.Add(help);
        Controls.Add(addRow);
        Controls.Add(typeBox);
        Controls.Add(Ui.ButtonBar(cancel, _go));

        _name.TextChanged += (_, _) => UpdateButtons();
        _kind.SelectedIndexChanged += (_, _) => SuggestName();
        _players.SelectedIndexChanged += (_, _) => UpdateButtons();
        _players.DoubleClick += (_, _) => RemovePlayer();

        foreach (var (name, kind) in previous) AddRow(name, kind);
        UpdateButtons();
        ResumeLayout(false);
        PerformLayout();
    }

    public List<(string Name, PlayerKind Kind)> Players { get; } = [];

    public GameType GameType => _tournament.Checked ? GameType.Tournament : GameType.Classic;

    private void SuggestName()
    {
        var kind = Kinds[_kind.SelectedIndex];
        if (kind == PlayerKind.Human)
        {
            if (_name.Text.StartsWith("Computer") || _name.Text.StartsWith("Claude")) _name.Text = "";
            return;
        }
        string stem = kind == PlayerKind.Claude ? "Claude" : "Computer";
        int n = 1;
        string candidate = stem;
        while (Names().Contains(candidate)) candidate = $"{stem} {++n}";
        _name.Text = candidate;
    }

    private HashSet<string> Names() => _players.Items.Cast<ListViewItem>().Select(i => i.Text).ToHashSet(StringComparer.OrdinalIgnoreCase);

    private void AddPlayer()
    {
        string name = _name.Text.Trim();
        var kind = Kinds[_kind.SelectedIndex];
        if (name.Length == 0) return;
        if (_players.Items.Count >= MaxPlayers)
        {
            MessageBox.Show(this, $"A game can have up to {MaxPlayers} players.", "New Game", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (Names().Contains(name))
        {
            MessageBox.Show(this, "Each player needs a different name.", "New Game", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        if (kind == PlayerKind.Claude && ClaudeCodeRunner.ResolveExecutable(_claudeSettings.ExecutablePath) is null)
        {
            MessageBox.Show(this, "Claude Code (claude.exe) wasn't found. Install Claude Code and sign in, or set its location in " +
                                  "Options > Preferences > Claude. Until then this player will use the built-in Hard strategy.",
                "Claude Code not found", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
        AddRow(name, kind);
        _name.Text = "";
        if (kind != PlayerKind.Human) SuggestName();
        _name.Focus();
        UpdateButtons();
    }

    private void AddRow(string name, PlayerKind kind)
    {
        _players.Items.Add(new ListViewItem([name, kind.Label()]) { Tag = kind });
    }

    private void RemovePlayer()
    {
        if (_players.SelectedIndices.Count == 0) return;
        _players.Items.RemoveAt(_players.SelectedIndices[0]);
        UpdateButtons();
    }

    private void MovePlayer(int delta)
    {
        if (_players.SelectedIndices.Count == 0) return;
        int i = _players.SelectedIndices[0];
        int j = i + delta;
        if (j < 0 || j >= _players.Items.Count) return;
        var item = _players.Items[i];
        _players.Items.RemoveAt(i);
        _players.Items.Insert(j, item);
        item.Selected = true;
        item.Focused = true;
    }

    private void UpdateButtons()
    {
        bool selected = _players.SelectedIndices.Count > 0;
        _remove.Enabled = _up.Enabled = _down.Enabled = selected;
        _go.Enabled = _players.Items.Count > 0;
        _add.Enabled = _name.Text.Trim().Length > 0;
        _tournament.Enabled = _players.Items.Count > 1;
        if (!_tournament.Enabled) _classic.Checked = true;
    }

    private void Start()
    {
        Players.Clear();
        foreach (ListViewItem item in _players.Items)
            Players.Add((item.Text, (PlayerKind)item.Tag!));
        DialogResult = DialogResult.OK;
        Close();
    }
}
