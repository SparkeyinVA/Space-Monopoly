using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.WinForms;

/// <summary>Set up players (human or computer) and the game type (Players.cpp in the original).</summary>
internal sealed class NewGameForm : Form
{
    private const int MaxPlayers = 8;

    private static readonly PlayerKind[] Kinds =
        [PlayerKind.Human, PlayerKind.ComputerEasy, PlayerKind.ComputerMedium, PlayerKind.ComputerNormal, PlayerKind.ComputerHard, PlayerKind.Claude];

    private readonly TextBox _name = new() { MaxLength = 15, Width = 150 };
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly ComboBox _personality = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 110 };
    private readonly ListView _players = Ui.Table(("Name", 140), ("Player type", 160), ("Personality", 110));

    // The personality list: none, random, then the four personalities.
    private static readonly string[] PersonalityChoices = ["None", "Random", .. Personalities.Playable.Select(p => p.ToString())];
    private readonly RadioButton _modern = new() { Text = "Modern — 25 rounds, a hand of squares, majority bonuses (about 15–30 minutes)", AutoSize = true };
    private readonly RadioButton _classic = new() { Text = "Classic — the 2001 rules: everyone shares one board", AutoSize = true };
    private readonly RadioButton _tournament = new() { Text = "Tournament — everyone gets their own copy of the same board", AutoSize = true };
    private readonly ComboBox _chaos = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 330 };
    private readonly CheckBox _advisor = new() { Text = "Advisor: let human players ask what Hard would play (counted in the high scores)", AutoSize = true };
    private bool _advisorTouched;
    private readonly CheckBox _adaptive = new() { AutoSize = true };
    private readonly Button _add, _remove, _up, _down, _go;
    private readonly ClaudeCodeSettings _claudeSettings;

    public NewGameForm(ClaudeCodeSettings claudeSettings, IReadOnlyList<PlayerSetup> previous, GameType lastType, Chaos lastChaos,
        bool adaptive = false, int adaptiveStep = 0)
    {
        _claudeSettings = claudeSettings;
        SuspendLayout();
        Text = "New Game";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Ui.KeepOnScreen(this);   // F17: never off the top of a second monitor
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(760, 590);

        foreach (var k in Kinds) _kind.Items.Add(k.Label());
        _kind.SelectedIndex = 0;
        _personality.Items.AddRange(PersonalityChoices);
        _personality.SelectedIndex = 0;
        _personality.Enabled = false;
        Ui.Tip(_personality, "How a computer or Claude player likes to play: Raider goes after mergers and majority bonuses, " +
                                               "Tycoon founds and holds companies, Hoarder keeps cash and avoids risk, Gambler bets big on one company.");

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
        addRow.Controls.AddRange([Ui.Label("Name:"), _name, Ui.Label("Plays as:"), _kind, _personality, _add]);
        foreach (Control c in addRow.Controls) c.Anchor = AnchorStyles.Left;

        var help = Ui.Label("Add players in the order they will play. Computer players can be mixed with humans; " +
                            "Claude players think using your Claude Code login.");
        help.MaximumSize = new Size(670, 0);
        help.Dock = DockStyle.Top;
        help.Padding = new Padding(8, 4, 8, 4);

        var side = new FlowLayoutPanel { Dock = DockStyle.Right, FlowDirection = FlowDirection.TopDown, AutoSize = true, Padding = new Padding(4) };
        side.Controls.AddRange([_up, _down, _remove]);

        var listHolder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 4, 0, 4) };
        listHolder.Controls.Add(_players);

        // Sized to its options, one under the other: with a fixed height the last option wrapped into a second column.
        var typeBox = new GroupBox { Text = "Game type", Dock = DockStyle.Bottom, AutoSize = true, AutoSizeMode = AutoSizeMode.GrowAndShrink, Padding = new Padding(10, 6, 10, 6) };
        var typeFlow = new FlowLayoutPanel { Dock = DockStyle.Top, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoSize = true };
        _chaos.Items.AddRange(["Calm — events are rare", "Standard — an event most rounds", "Wild — something happens almost every round"]);
        _chaos.SelectedIndex = (int)lastChaos;
        var chaosRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(18, 0, 0, 0) };
        var chaosLabel = Ui.Label("Modern events:");
        chaosLabel.Anchor = _chaos.Anchor = AnchorStyles.Left;
        chaosRow.Controls.AddRange([chaosLabel, _chaos]);
        _adaptive.Text = $"Adaptive difficulty: computer players get stronger when you win, weaker when you lose " +
                         $"(now: {AdaptiveDifficulty.At(adaptiveStep).Label})";
        _adaptive.Checked = adaptive;
        Ui.Tip(_adaptive, "Computer players (not Claude) play at the current step, whatever level they were added with. " +
                          "Ladder: " + string.Join(" → ", AdaptiveDifficulty.Steps.Select(s => s.Label)) + ".");
        typeFlow.Controls.AddRange([_modern, chaosRow, _classic, _tournament, _advisor, _adaptive]);
        _advisor.Margin = new Padding(3, 6, 3, 0);
        _advisor.Click += (_, _) => _advisorTouched = true;
        _modern.CheckedChanged += (_, _) => _chaos.Enabled = _modern.Checked;
        (lastType switch { GameType.Classic => _classic, GameType.Tournament => _tournament, _ => _modern }).Checked = true;
        _chaos.Enabled = _modern.Checked;
        typeBox.Controls.Add(typeFlow);

        Controls.Add(listHolder);
        Controls.Add(side);
        Controls.Add(help);
        Controls.Add(addRow);
        Controls.Add(typeBox);
        Controls.Add(Ui.ButtonBar(cancel, _go));

        _name.TextChanged += (_, _) => UpdateButtons();
        _kind.SelectedIndexChanged += (_, _) =>
        {
            SuggestName();
            _personality.Enabled = Kinds[_kind.SelectedIndex] != PlayerKind.Human;
            if (!_personality.Enabled) _personality.SelectedIndex = 0;
        };
        _players.SelectedIndexChanged += (_, _) => UpdateButtons();
        _players.DoubleClick += (_, _) => RemovePlayer();

        foreach (var p in previous) AddRow(p);
        UpdateButtons();
        ResumeLayout(false);
        PerformLayout();
    }

    public List<PlayerSetup> Players { get; } = [];

    public Chaos Chaos => (Chaos)Math.Max(0, _chaos.SelectedIndex);

    /// <summary>Whether adaptive difficulty is on (S9).</summary>
    public bool Adaptive => _adaptive.Checked;

    /// <summary>Whether human players may use the Advisor this game (S14).</summary>
    public bool Advisor => _advisor.Checked;

    public GameType GameType => _tournament.Checked ? GameType.Tournament : _classic.Checked ? GameType.Classic : GameType.Modern;

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
        Personality? personality = kind == PlayerKind.Human ? Personality.None
            : _personality.SelectedIndex switch { 0 => Personality.None, 1 => null, var i => Personalities.Playable[i - 2] };
        AddRow(new PlayerSetup(name, kind, personality));
        _name.Text = "";
        if (kind != PlayerKind.Human) SuggestName();
        _name.Focus();
        UpdateButtons();
    }

    private void AddRow(PlayerSetup setup)
    {
        string personality = setup.Personality is Personality p ? (p == Personality.None ? "" : p.ToString()) : "Random";
        _players.Items.Add(new ListViewItem([setup.Name, setup.Kind.Label(), personality]) { Tag = setup });
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
        // The Advisor is on by default when no opponent is stronger than Medium, until the player chooses.
        if (!_advisorTouched)
            _advisor.Checked = _players.Items.Cast<ListViewItem>().Select(i => ((PlayerSetup)i.Tag!).Kind)
                .All(k => k is PlayerKind.Human or PlayerKind.ComputerEasy or PlayerKind.ComputerMedium);
        bool selected = _players.SelectedIndices.Count > 0;
        _remove.Enabled = _up.Enabled = _down.Enabled = selected;
        _go.Enabled = _players.Items.Count > 0;
        _add.Enabled = _name.Text.Trim().Length > 0;
        _tournament.Enabled = _players.Items.Count > 1;
        if (!_tournament.Enabled && _tournament.Checked) _classic.Checked = true;
    }

    private void Start()
    {
        Players.Clear();
        foreach (ListViewItem item in _players.Items)
            Players.Add((PlayerSetup)item.Tag!);
        DialogResult = DialogResult.OK;
        Close();
    }
}

/// <summary>A player set up in New Game. A null personality means "Random", decided when the game starts.</summary>
internal sealed record PlayerSetup(string Name, PlayerKind Kind, Personality? Personality = Personality.None);
