using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.WinForms;

/// <summary>The main window (SMMain in the original) and the turn loop for human and computer players.</summary>
internal sealed class MainForm : Form
{
    private readonly Storage _storage = Storage.Default;
    private readonly Random _rng = new();
    private AppSettings _settings;
    private HighScoreTable _highScores;

    private Board _board;
    private Game? _game;
    private IAiPlayer?[] _ais = [];
    private CancellationTokenSource? _cts;
    private bool _computerBusy;
    private List<PlayerSetup> _lastPlayers = [new("Player 1", PlayerKind.Human), new("Computer", PlayerKind.ComputerMedium)];

    /// <summary>Each player's personality in the current game (Random already decided).</summary>
    private Personality[] _personalities = [];

    // Menu
    private readonly ToolStripMenuItem _newGame, _endGame, _newGrid, _gridSize, _preferences;
    private readonly ToolStripMenuItem[] _sizeItems;

    // Side panel
    private readonly BoardView _boardView = new() { Dock = DockStyle.Fill };
    private readonly Label _goLabel = Ui.Label("", 10f, FontStyle.Bold);
    private readonly Label _playerLabel = Ui.Label("", 14f, FontStyle.Bold);
    private readonly Label _kindLabel = Ui.Label("");
    private readonly Label _moneyLabel = Ui.Label("", 14f, FontStyle.Bold);
    private readonly Label _eventLabel = Ui.Label("");
    private readonly Label _status = Ui.Label("");

    // F13: the player's hand, one row per square with what it does and Play / Swap buttons, plus New hand (F12).
    private readonly TableLayoutPanel _handPanel = new() { AutoSize = true, ColumnCount = 3, Margin = new Padding(0, 4, 0, 0), Visible = false };
    private readonly Button _newHand = new() { AutoSize = true, Margin = new Padding(2) };
    private string _handKey = "";
    private readonly CompanyButton[] _companyButtons = new CompanyButton[Company.MaxCount];
    private readonly Panel[] _companyDots = new Panel[Company.MaxCount];
    // Not auto-sized: an auto-sized table grows to fit the button text and pushes the second column out of view.
    private readonly TableLayoutPanel _companyGrid = new() { Dock = DockStyle.Fill };
    private readonly TableLayoutPanel _side = new()
    {
        Dock = DockStyle.Right,
        Width = 360,
        ColumnCount = 1,
        Padding = new Padding(8),
        BackColor = SystemColors.Control,
    };
    private readonly GroupBox _companyBox = new() { Text = "Companies (click for details and trading)", Dock = DockStyle.Fill, Padding = new Padding(6) };
    private int _companySlots = -1;
    private readonly Button _endTurn;
    private readonly FlowLayoutPanel _info = new() { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
    private readonly Button _market;
    private readonly ToolStripMenuItem _daily;
    private readonly ToolStripMenuItem _fast;
    private const int FastDelayMs = 150;

    // S8: a news line above the board for new companies, mergers, splits and events, which fades instead of stopping play.
    private readonly Label _news = new()
    {
        Dock = DockStyle.Top,
        AutoSize = false,
        Height = 30,
        TextAlign = ContentAlignment.MiddleLeft,
        Padding = new Padding(10, 0, 10, 0),
        BackColor = Color.FromArgb(14, 18, 44),
        ForeColor = Color.FromArgb(14, 18, 44),
        Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
        AutoEllipsis = true,
    };
    private readonly System.Windows.Forms.Timer _newsTimer = new() { Interval = 100 };
    private int _newsAge;
    private readonly Button _advisor;
    private bool _advisorEnabled;
    private int[] _advisorUses = [];
    private int? _adaptiveStep;
    private Pos? _advisedSquare;
    private readonly TableLayoutPanel _turnButtons = new() { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1, Margin = Padding.Empty };
    private readonly ListView _playerList = Ui.Table(("Player", 120), ("Net worth", 110));
    private readonly TextBox _log = new()
    {
        Multiline = true,
        ReadOnly = true,
        ScrollBars = ScrollBars.Vertical,
        Dock = DockStyle.Fill,
        BackColor = SystemColors.Window,
        Font = new Font("Segoe UI", 8.5f),
    };

    public MainForm()
    {
        _settings = _storage.LoadSettings();
        _highScores = _storage.LoadHighScores();

        // Scaling for high-DPI screens is applied when layout resumes at the end of the constructor.
        SuspendLayout();
        Text = "Space Monopoly";
        Icon = Icon.ExtractAssociatedIcon(Application.ExecutablePath);
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(1100, 900);
        MinimumSize = new Size(760, 560);
        StartPosition = FormStartPosition.CenterScreen;

        // ---- menu
        var menu = new MenuStrip();
        _newGame = new ToolStripMenuItem("&New Game...", null, (_, _) => NewGame(), Keys.Control | Keys.N);
        _endGame = new ToolStripMenuItem("&End Game", null, (_, _) => EndGameEarly());
        var scores = new ToolStripMenuItem("View &High Scores", null, (_, _) => ShowHighScores([]));
        var quit = new ToolStripMenuItem("E&xit", null, (_, _) => Close());
        _daily = new ToolStripMenuItem("&Daily Challenge", null, (_, _) => DailyChallenge());
        var project = new ToolStripMenuItem("&Game", null, _newGame, _daily, _endGame, new ToolStripSeparator(), scores, new ToolStripSeparator(), quit);

        _newGrid = new ToolStripMenuItem("&Create New Grid", null, (_, _) => NewGrid(), Keys.F5);
        _sizeItems = Enum.GetValues<GridSize>()
            .Select(size => new ToolStripMenuItem(GridSpec.For(size).Label, null, (_, _) => SetGridSize(size)) { Tag = size })
            .ToArray();
        _gridSize = new ToolStripMenuItem("&Grid Size", null, _sizeItems);
        _preferences = new ToolStripMenuItem("&Preferences...", null, (_, _) => ShowPreferences());
        _fast = new ToolStripMenuItem("&Fast computer players", null, (_, _) => ToggleFast(), Keys.Control | Keys.F) { Checked = _settings.ComputerDelayMs <= FastDelayMs };
        var options = new ToolStripMenuItem("&Options", null, _newGrid, _gridSize, new ToolStripSeparator(), _fast, _preferences);

        var howTo = new ToolStripMenuItem("&How to Play", null, (_, _) => HelpForms.ShowHowToPlay(this, _game?.Type ?? _settings.LastGameType, Currency), Keys.F1);
        var about = new ToolStripMenuItem("&About Space Monopoly", null, (_, _) => HelpForms.ShowAbout(this));
        var achievementsItem = new ToolStripMenuItem("&Achievements", null, (_, _) => ShowAchievements());
        var help = new ToolStripMenuItem("&Help", null, howTo, achievementsItem, about);
        menu.Items.AddRange([project, options, help]);
        MainMenuStrip = menu;

        // ---- side panel
        _endTurn = new Button
        {
            Text = "End Turn",
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = new Padding(4, 8, 4, 8),
        };
        _endTurn.Click += async (_, _) => await HumanEndsTurnAsync();
        _market = new Button { Text = "&Market", Dock = DockStyle.Fill, Margin = new Padding(4, 8, 4, 8), Enabled = false };
        _market.Click += (_, _) => OpenMarket();
        Ui.Tip(_market, "Buy and sell every company in one window (M).");
        _advisor = new Button { Text = "&Advisor", Dock = DockStyle.Fill, Margin = new Padding(4, 8, 4, 8), Visible = false };
        _advisor.Click += (_, _) => ShowAdvice();
        Ui.Tip(_advisor, "What would the Hard computer player do now, and why? (A)");
        _turnButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0));
        _turnButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0));
        _turnButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 30));
        _turnButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 70));
        _turnButtons.Controls.Add(_advisor, 1, 0);
        _turnButtons.Controls.Add(_market, 2, 0);
        _turnButtons.Controls.Add(_endTurn, 3, 0);

        var side = _side;
        side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Fixed rows are given their heights after DPI scaling.
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // info
        side.RowStyles.Add(new RowStyle(SizeType.Absolute));       // companies
        side.RowStyles.Add(new RowStyle(SizeType.Absolute));       // end turn
        side.RowStyles.Add(new RowStyle(SizeType.Absolute));       // players (sized in SizePlayersBox)
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));  // log (sized in SizeLog: the space left, at least a minimum)
        side.AutoScroll = true;   // when everything doesn't fit, the side panel scrolls instead of hiding the players and log
        side.Resize += (_, _) => SizeLog();

        var info = _info;
        // Long labels wrap instead of widening the side panel past the window edge.
        _goLabel.MaximumSize = _playerLabel.MaximumSize = new Size(330, 0);
        _status.MaximumSize = new Size(300, 0);
        _status.ForeColor = Color.FromArgb(40, 60, 140);
        _kindLabel.MaximumSize = new Size(330, 0);
        _eventLabel.ForeColor = Color.FromArgb(150, 60, 0);
        _eventLabel.MaximumSize = new Size(330, 0);
        info.Controls.AddRange([_goLabel, _eventLabel, _playerLabel, _kindLabel, _moneyLabel, _status, _handPanel]);
        _handPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _handPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _handPanel.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        _newHand.Click += (_, _) => HumanProspects();

        for (int c = 0; c < Company.MaxCount; c++)
        {
            int index = c;
            // Sized in ShowCompanySlots: these are added after the form has been scaled for the screen's DPI.
            var dot = new Panel { Anchor = AnchorStyles.None, Margin = Padding.Empty, BackColor = Color.FromArgb(Company.Colors[c]) };
            dot.Paint += (_, e) => PaintDot(e.Graphics, index, dot.ClientRectangle);
            var button = new CompanyButton { Dock = DockStyle.Fill, Margin = new Padding(2) };
            button.Click += (_, _) => OpenCompany(index);
            _companyDots[c] = dot;
            _companyButtons[c] = button;
        }
        _companyBox.Controls.Add(_companyGrid);

        _playerList.DoubleClick += (_, _) => ShowPlayerDetails();
        var playersBox = new GroupBox { Text = "Players (double-click for details)", Dock = DockStyle.Fill, Padding = new Padding(6) };
        playersBox.Controls.Add(_playerList);

        var logBox = new GroupBox { Text = "Game log", Dock = DockStyle.Fill, Padding = new Padding(6) };
        logBox.Controls.Add(_log);

        side.Controls.Add(info, 0, 0);
        side.Controls.Add(_companyBox, 0, 1);
        side.Controls.Add(_turnButtons, 0, 2);
        side.Controls.Add(playersBox, 0, 3);
        side.Controls.Add(logBox, 0, 4);

        _boardView.SquareClicked += (_, p) => HumanChooses(p);
        _boardView.SquareRightClicked += (_, p) => HumanSwaps(p);

        Controls.Add(_boardView);
        Controls.Add(_news);
        _newsTimer.Tick += (_, _) => FadeNews();
        Controls.Add(side);
        Controls.Add(menu);
        ResumeLayout(false);
        PerformLayout();
        _side.RowStyles[2].Height = LogicalToDeviceUnits(56);

        _board = NewBoard(_settings.GridSize);
        RefreshAll();
        Log("Welcome to Space Monopoly! Choose Game > New Game to start. " +
            "Use Options > Create New Grid until you like the look of the board — lots of planets close together is best.");
    }

    /// <summary>Keeps the window on screen: it opens tall enough for twelve companies and eight players when there is room.</summary>
    protected override void OnLoad(EventArgs e)
    {
        base.OnLoad(e);
        var area = Screen.FromControl(this).WorkingArea;
        Size = new Size(Math.Min(Width, area.Width), Math.Min(Height, area.Height));
        Location = new Point(area.Left + (area.Width - Width) / 2, area.Top + (area.Height - Height) / 2);
    }

    private Currency Currency => _settings.Currency;

    /// <summary>
    /// Shows one button per company the grid size allows: one column for up to six,
    /// two columns with the price on a second line for more.
    /// </summary>
    private void ShowCompanySlots(int count)
    {
        if (count == _companySlots) return;
        _companySlots = count;
        bool twoColumns = count > 6;
        int perColumn = twoColumns ? (count + 1) / 2 : count;

        _companyGrid.SuspendLayout();
        _companyGrid.Controls.Clear();
        _companyGrid.ColumnStyles.Clear();
        _companyGrid.RowStyles.Clear();
        _companyGrid.ColumnCount = twoColumns ? 4 : 2;
        _companyGrid.RowCount = perColumn;
        for (int col = 0; col < _companyGrid.ColumnCount / 2; col++)
        {
            _companyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, LogicalToDeviceUnits(26)));
            _companyGrid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
        }
        int rowHeight = LogicalToDeviceUnits(twoColumns ? 42 : 36);
        for (int row = 0; row < perColumn; row++)
            _companyGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, rowHeight));
        // Measured from the top and padding, as the box may not have been laid out yet.
        _side.RowStyles[1].Height = perColumn * rowHeight + _companyBox.DisplayRectangle.Top + _companyBox.Padding.Bottom
                                    + LogicalToDeviceUnits(2) + _companyBox.Margin.Vertical;
        for (int c = 0; c < count; c++)
        {
            _companyDots[c].Size = LogicalToDeviceUnits(new Size(20, 20));
            _companyGrid.Controls.Add(_companyDots[c], c / perColumn * 2, c % perColumn);
            _companyGrid.Controls.Add(_companyButtons[c], c / perColumn * 2 + 1, c % perColumn);
        }
        _companyGrid.ResumeLayout(true);
    }
    private GridSpec Spec => GridSpec.For(_settings.GridSize);
    private bool InProgress => _game is { Phase: not GamePhase.Over };

    // ------------------------------------------------------------------ board and menus

    /// <summary>Planets per square on the current grid: Modern boards have more (about 1 in 12, against 1 in 30).</summary>
    private int _boardPlanetOdds;

    private static int PlanetOddsFor(GameType type) => type == GameType.Modern ? ModernRules.PlanetOdds : Board.CircleOdds;

    /// <summary>A new random grid, with as many planets as the last game type picked in New Game uses.</summary>
    private Board NewBoard(GridSize size)
    {
        var spec = GridSpec.For(size);
        var board = new Board(spec.Width, spec.Height);
        _boardPlanetOdds = PlanetOddsFor(_settings.LastGameType);
        board.Randomise(_rng, _boardPlanetOdds);
        // S11: Modern's special squares, on the medium and large grids.
        if (_settings.LastGameType == GameType.Modern && size != GridSize.Small) board.PlaceSpecials(_rng);
        return board;
    }

    private void NewGrid()
    {
        if (InProgress) return;
        _game = null;
        _board = NewBoard(_settings.GridSize);
        RefreshAll();
    }

    private void SetGridSize(GridSize size)
    {
        if (InProgress) return;
        _settings = _settings with { GridSize = size };
        _storage.SaveSettings(_settings);
        NewGrid();
    }

    private void ShowPreferences()
    {
        using var form = new PreferencesForm(_settings);
        if (form.ShowDialog(this) != DialogResult.OK) return;
        _settings = form.Settings;
        _storage.SaveSettings(_settings);
        RefreshAll();
    }

    private void ShowHighScores(IReadOnlyCollection<(GridSize Size, bool Modern, int Index)> fresh, bool? modern = null)
    {
        using var form = new HighScoresForm(_highScores, Currency, _settings.GridSize, modern ?? _settings.LastGameType == GameType.Modern, fresh);
        form.ShowDialog(this);
    }

    // ------------------------------------------------------------------ starting and ending games

    private async void NewGame()
    {
        if (InProgress) return;
        using var form = new NewGameForm(_settings.Claude, _lastPlayers, _settings.LastGameType, _settings.LastChaos,
            _settings.AdaptiveDifficulty, _settings.AdaptiveStep);
        if (form.ShowDialog(this) != DialogResult.OK || form.Players.Count == 0) return;
        _lastPlayers = form.Players;
        if (form.GameType != _settings.LastGameType || form.Chaos != _settings.LastChaos || form.Adaptive != _settings.AdaptiveDifficulty)
        {
            _settings = _settings with { LastGameType = form.GameType, LastChaos = form.Chaos, AdaptiveDifficulty = form.Adaptive };
            _storage.SaveSettings(_settings);
        }
        _dailyDate = null;
        await StartGame(form.Players, form.GameType, form.Chaos, form.Advisor, form.Adaptive);
    }

    /// <summary>
    /// The Daily Challenge (S10): a Modern game on the medium grid against a Hard Raider and a Normal Tycoon, with the
    /// board, the squares dealt and the computer players all seeded from today's date, so every attempt today is the same game.
    /// </summary>
    private async void DailyChallenge()
    {
        if (InProgress) return;
        var today = DateTime.Today;
        string name = _lastPlayers.FirstOrDefault(p => p.Kind == PlayerKind.Human)?.Name ?? "You";
        _dailyDate = today;
        // F15: the same game for everyone today: fixed rules (not the player's Preferences) and the medium grid,
        // without changing the player's own grid size.
        await StartGame(
            [new(name, PlayerKind.Human), new("Hard", PlayerKind.ComputerHard, Personality.Raider), new("Normal", PlayerKind.ComputerNormal, Personality.Tycoon)],
            GameType.Modern, Chaos.Standard, advisor: false, adaptive: false, Replay.DailyBoard(today), Replay.DailySeed(today),
            new GameRules { Chaos = Chaos.Standard }, GridSpec.For(GridSize.Medium));
        var best = _storage.LoadDailyBests().GetValueOrDefault(today.ToString("yyyy-MM-dd"));
        Log($"Daily Challenge for {today:D}: everyone gets the same board, squares and opponents today." +
            (best > 0 ? $" Your best today: {Currency.Format(best)}." : ""));
    }

    private DateTime? _dailyDate;

    private async Task StartGame(IReadOnlyList<PlayerSetup> players, GameType type, Chaos chaos, bool advisor, bool adaptive,
        Board? fixedBoard = null, int? seed = null, GameRules? rules = null, GridSpec? gridSpec = null)
    {
        var spec = gridSpec ?? Spec;
        _adaptiveStep = adaptive ? _settings.AdaptiveStep : null;
        var step = AdaptiveDifficulty.At(_settings.AdaptiveStep);
        // Adaptive difficulty: built-in computer players play at the current step (Claude players are unchanged).
        var setups = players.Select(p => adaptive && p.Kind.IsComputer() && p.Kind != PlayerKind.Claude ? p with { Kind = step.Kind } : p).ToList();
        _log.Clear();

        if (fixedBoard is not null)
        {
            _board = fixedBoard;
            _boardPlanetOdds = PlanetOddsFor(type);
        }
        // Modern boards have more planets. Switching between Modern and the 2001 rules draws a new grid, and so does a
        // board left at another size (after a Daily Challenge, which is always on the medium grid).
        else if (_boardPlanetOdds != PlanetOddsFor(type) || _board.Width != spec.Width || _board.Height != spec.Height)
        {
            _board = NewBoard(_settings.GridSize);
            Log(type == GameType.Modern
                ? "A new grid with more planets was drawn for the Modern game."
                : "A new grid with the original number of planets was drawn for this game.");
        }

        // The game starts from the planets on the current grid; any companies left from the last game are cleared.
        _game = new Game(spec, _board, setups.Select(p => (p.Name, p.Kind)), type, rules ?? _settings.Rules with { Chaos = chaos }, seed);
        _personalities = setups.Select(p => p.Personality ?? Personalities.Random(_rng)).ToArray();
        _advisorEnabled = advisor;
        _advisorUses = new int[_game.Players.Count];
        _advisedSquare = null;
        _ais = _game.Players.Select((p, i) => !p.Kind.IsComputer() ? null
            : _adaptiveStep is int s && p.Kind != PlayerKind.Claude ? AdaptiveDifficulty.CreatePlayer(s, seed + i, _personalities[i])
            : AiFactory.Create(p.Kind, _settings.Claude, seed + i, _personalities[i])).ToArray();
        _cts = new CancellationTokenSource();

        string who = string.Join(", ", _game.Players.Select((p, i) =>
            $"{p.Name} ({p.Kind.Label()}{(_personalities[i] == Personality.None ? "" : $", {_personalities[i]}: {Personalities.Description(_personalities[i])}")})"));
        string goes = $"Each player gets {_game.GoesPerPlayer} goes";
        if (_game.GoesReduced)
            goes += $" (fewer than the usual {_game.MaxGoes}, so that everyone's goes fit on the board)";
        Log($"New {_game.Type.ToString().ToLowerInvariant()} game on the {spec.Label.ToLowerInvariant()} grid: {who}. " +
            $"{goes}, and there can be up to {spec.Companies} companies.");
        if (_game.IsModern)
            Log($"Modern rules: you hold a hand of {ModernRules.HandSize} squares (swap one a turn, or take a whole new hand once per game). Growth raises share prices but pays " +
                $"no dividends. Buy up to {ModernRules.BuyLimitPerTurn} shares a turn; each company has {ModernRules.SharesPerCompany:N0}. " +
                $"In a merger the two largest holders of the company taken over get {ModernRules.LargestHolderBonus}× and " +
                $"{ModernRules.SecondHolderBonus}× its share price as a bonus. Cash earns {ModernRules.InterestPercent}% interest each turn. " +
                $"Each round an event card is flipped, and the next one is shown in advance ({chaos.ToString().ToLowerInvariant()} events).");
        if (_adaptiveStep is int current)
            Log($"Adaptive difficulty: computer players are at step {current + 1} of {AdaptiveDifficulty.Steps.Count} ({AdaptiveDifficulty.At(current).Label}).");
        ReportRoundStart(_game);

        await ContinueAsync();
    }

    private void ShowAchievements()
    {
        var earned = _storage.LoadAchievements();
        var table = Ui.Table(("Achievement", 150), ("How to earn it", 330), ("Earned", 100));
        foreach (var a in Replay.Achievements)
            table.Items.Add(Ui.Row((earned.ContainsKey(a.Id) ? "⭐ " : "") + a.Title, a.Description,
                earned.TryGetValue(a.Id, out var when) ? when.ToShortDateString() : ""));
        Dialogs.Show(this, "Achievements", Color.Gold, [$"You have earned {earned.Count} of {Replay.Achievements.Count} achievements."], table);
    }

    private void EndGameEarly()
    {
        if (!InProgress) return;
        if (MessageBox.Show(this, "End the game now? Final standings will be calculated and high scores recorded.",
                "End Game", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        _cts?.Cancel();
        _game!.EndNow();
        FinishGame();
    }

    private void FinishGame()
    {
        var game = _game!;
        _boardView.AcceptClicks = false;
        RefreshAll();
        Log($"Game over after round {game.Turn}.");

        // Adaptive difficulty: one step up after a human win, one down after a loss.
        if (_adaptiveStep is int was && game.Players.Any(p => p.IsHuman))
        {
            bool humanWon = game.Standings()[0].Kind == PlayerKind.Human;
            int next = AdaptiveDifficulty.Next(was, humanWon);
            _settings = _settings with { AdaptiveStep = next };
            _storage.SaveSettings(_settings);
            Log(next == was
                ? $"Adaptive difficulty stays at {AdaptiveDifficulty.At(next).Label}."
                : $"Adaptive difficulty: {(humanWon ? "up" : "down")} to {AdaptiveDifficulty.At(next).Label} for the next game.");
        }
        foreach (var e in game.GameEndEvents) Log(EventText.Describe(game, e, Currency));

        var extras = new List<string>();
        var achievements = _storage.LoadAchievements();
        bool newAchievement = false;
        for (int p = 0; p < game.Players.Count; p++)
        {
            if (!game.Players[p].IsHuman) continue;
            foreach (var a in Replay.Earned(game, p, p < _advisorUses.Length ? _advisorUses[p] : 0, _dailyDate is not null))
                if (achievements.TryAdd(a.Id, DateTime.Now))
                {
                    newAchievement = true;
                    extras.Add($"⭐ New achievement for {game.Players[p].Name}: {a.Title} ({a.Description})");
                }
        }
        if (newAchievement) _storage.SaveAchievements(achievements);

        if (_dailyDate is DateTime day && game.Players.Select((pl, i) => (pl, i)).FirstOrDefault(t => t.pl.IsHuman) is { pl: not null } human)
        {
            var bests = _storage.LoadDailyBests();
            string key = day.ToString("yyyy-MM-dd");
            var worth = game.NetWorth(human.i);
            if (worth > bests.GetValueOrDefault(key))
            {
                extras.Insert(0, $"📅 New personal best for today's Daily Challenge: {Currency.Format(worth)}!");
                bests[key] = worth;
                _storage.SaveDailyBests(bests);
            }
            else
            {
                extras.Insert(0, $"📅 Today's Daily Challenge best is still {Currency.Format(bests[key])}.");
            }
        }
        foreach (var line in extras) Log(line);
        using (var over = new GameOverForm(game, Currency, Replay.Awards(game, Currency), extras))
            over.ShowDialog(this);

        var fresh = new List<(GridSize Size, bool Modern, int Index)>();
        foreach (var s in game.Standings())
        {
            int index = _highScores.Add(game.Spec.Size, new HighScoreEntry(s.Name, s.NetWorth, s.Kind, game.Type, DateTime.Now,
                s.Player < _advisorUses.Length ? _advisorUses[s.Player] : 0,
                _adaptiveStep is int played && s.Kind == PlayerKind.Human ? $"adaptive: {AdaptiveDifficulty.At(played).Label}" : null));
            if (index >= 0)
            {
                // Earlier new entries move down when a later one is inserted above them.
                for (int i = 0; i < fresh.Count; i++)
                    if (fresh[i].Index >= index) fresh[i] = fresh[i] with { Index = fresh[i].Index + 1 };
                fresh.Add((game.Spec.Size, game.IsModern, index));
            }
        }
        fresh.RemoveAll(f => f.Index >= HighScoreTable.MaxEntries);
        _storage.SaveHighScores(_highScores);
        ShowHighScores(fresh, game.IsModern);
        RefreshAll();
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        string message = InProgress
            ? "If you quit now your score will not be recorded in the High Scores table. Quit Space Monopoly anyway?"
            : "Are you sure you want to quit?";
        if (e.CloseReason == CloseReason.UserClosing &&
            MessageBox.Show(this, message, "Quit Space Monopoly", MessageBoxButtons.YesNo,
                InProgress ? MessageBoxIcon.Exclamation : MessageBoxIcon.Question) != DialogResult.Yes)
        {
            e.Cancel = true;
            return;
        }
        _cts?.Cancel();
        base.OnFormClosing(e);
    }

    // ------------------------------------------------------------------ the turn loop

    /// <summary>Plays computer turns until it's a human's turn or the game ends.</summary>
    private async Task ContinueAsync()
    {
        var game = _game;
        var token = _cts?.Token ?? CancellationToken.None;
        if (game is null) return;

        try
        {
            while (game.Phase != GamePhase.Over && _ais[game.CurrentPlayerIndex] is IAiPlayer ai)
            {
                await PlayComputerTurnAsync(game, ai, token);
                if (token.IsCancellationRequested || game != _game) return;
                game.EndTurn();
                ReportRoundStart(game);
            }
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException)
        {
            Log("Error: " + ex.Message);
            MessageBox.Show(this, "Something went wrong during a computer player's turn:" + Environment.NewLine + ex.Message,
                "Space Monopoly", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }
        finally
        {
            _computerBusy = false;
        }

        if (game != _game) return;
        if (game.Phase == GamePhase.Over)
        {
            FinishGame();
            return;
        }

        // A human's turn.
        _boardView.AcceptClicks = true;
        RefreshAll();
    }

    private async Task PlayComputerTurnAsync(Game game, IAiPlayer ai, CancellationToken token)
    {
        _computerBusy = true;
        _boardView.AcceptClicks = false;
        var player = game.CurrentPlayer;
        bool isClaude = player.Kind == PlayerKind.Claude;
        SetStatus(isClaude ? $"{player.Name} is asking Claude for a move — this usually takes 10-30 seconds..." : $"{player.Name} is thinking...");
        RefreshAll();

        await Task.Delay(_settings.ComputerDelayMs, token);
        bool couldProspect = game.CanProspect;
        var square = await ai.ChooseSquareAsync(game, token);
        token.ThrowIfCancellationRequested();
        if (couldProspect && game.HasProspected(game.CurrentPlayerIndex))
            Log($"{player.Name} took a new hand: threw away all their squares and drew new ones.");

        // Show which square was picked before playing it. A Modern hand stays private: only the chosen square shows.
        if (game.IsModern)
            _boardView.ShowBoard(game.Board, [square], square, ChoiceMarks(game).Where((_, i) => game.Choices[i] == square).ToList());
        else
            _boardView.ShowBoard(game.Board, game.Choices, square, ChoiceMarks(game));
        await Task.Delay(_settings.ComputerDelayMs, token);

        var events = game.PlaceSquare(square);
        if (!string.IsNullOrWhiteSpace(ai.LastComment)) Log($"{player.Name}: \"{ai.LastComment}\"");
        ReportEvents(game, events, popups: true);
        SettleConversions(game, events);
        RefreshAll();

        var orders = await ai.PlanTradesAsync(game, token);
        var trades = TradeExecutor.Apply(game, orders);
        foreach (var t in trades) Log(EventText.Describe(game, t, Currency));
        if (trades.Count == 0) Log($"{player.Name} made no trades.");
        RefreshAll();

        await Task.Delay(_settings.ComputerDelayMs, token);
    }

    private void HumanChooses(Pos square)
    {
        var game = _game;
        if (game is null || _computerBusy || game.Phase != GamePhase.ChoosingSquare) return;

        if (game.ValidateChoice(square) is string error)
        {
            MessageBox.Show(this, error, "Game Information", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
        }

        _boardView.AcceptClicks = false;
        var events = game.PlaceSquare(square);
        RefreshAll();
        ReportEvents(game, events, popups: true);
        SettleConversions(game, events);

        // Like the original, open the new company's details so its founder can buy shares straight away.
        if (events.OfType<CompanyFoundedEvent>().LastOrDefault() is { } founded && game.Companies[founded.Company].Exists)
            if (!_settings.OpenMarketAfterSquare) OpenCompany(founded.Company);
        if (_settings.OpenMarketAfterSquare && game.Phase == GamePhase.Trading) OpenMarket();

        RefreshAll();
    }

    private void HumanSwaps(Pos square)
    {
        var game = _game;
        if (game is null || _computerBusy || !game.CurrentPlayer.IsHuman || !game.IsModern) return;
        if (!game.CanSwap)
        {
            SetStatus("You've already swapped a square this turn. Play one of the squares in your hand.");
            return;
        }
        int number = game.Choices.ToList().IndexOf(square) + 1;
        game.Swap(square);
        Log($"{game.CurrentPlayer.Name} swapped square {number} for a new one.");
        RefreshAll();
    }

    /// <summary>
    /// F13: lists the human's hand in the side panel, each square with what it would do and Play / Swap buttons,
    /// how many swaps are left this turn and the New hand button (F12). Pointing at a row picks out its square.
    /// </summary>
    private void ShowHand(Game? game, bool inProgress)
    {
        bool show = game is not null && inProgress && game.IsModern && !_computerBusy && game.CurrentPlayer.IsHuman && game.Phase == GamePhase.ChoosingSquare;
        string key = show ? $"{game!.CurrentPlayerIndex}|{game.Turn}|{string.Join(";", game.Choices)}|{game.CanSwap}|{game.CanProspect}" : "";
        if (key == _handKey) return;
        _handKey = key;

        _handPanel.SuspendLayout();
        foreach (Control c in _handPanel.Controls.Cast<Control>().ToList())
            if (c != _newHand) c.Dispose();
        _handPanel.Controls.Clear();
        _handPanel.RowStyles.Clear();
        _handPanel.Visible = show;
        _boardView.Highlight = null;
        if (!show)
        {
            _handPanel.ResumeLayout();
            return;
        }

        var header = Ui.Label($"Your hand · swaps left this turn: {(game!.CanSwap ? 1 : 0)}", 9f, FontStyle.Bold);
        _handPanel.Controls.Add(header, 0, 0);
        _handPanel.SetColumnSpan(header, 3);

        var previews = Previews(game);
        for (int i = 0; i < game.Choices.Count; i++)
        {
            var square = game.Choices[i];
            var preview = previews[i];
            int left = game.TurnsLeft(game.CurrentPlayerIndex, square);
            var text = Ui.Label($"{i + 1}. {preview.Summary(Currency, game.CurrentPlayerIndex)}" + (preview.Allowed is null ? "" : " (not allowed)") +
                                (left <= 1 ? " · last turn" : left < 100 ? $" · {left} turns left" : ""));
            if (left <= 1) text.ForeColor = Color.FromArgb(190, 80, 0);
            text.MaximumSize = new Size(LogicalToDeviceUnits(230), 0);
            text.Anchor = AnchorStyles.Left;
            var play = new Button { Text = "Play", AutoSize = true, Margin = new Padding(2), Enabled = preview.Allowed is null };
            var swap = new Button { Text = "Swap", AutoSize = true, Margin = new Padding(2), Enabled = game.CanSwap };
            play.Click += (_, _) => HumanChooses(square);
            swap.Click += (_, _) => HumanSwaps(square);
            Ui.Tip(text, preview.Details(game, Currency));
            Ui.Tip(swap, "Throw this square away and draw a new one (one swap a turn). Right-clicking the square does the same.");
            foreach (Control c in new Control[] { text, play, swap })
            {
                c.MouseEnter += (_, _) => _boardView.Highlight = square;
                c.MouseLeave += (_, _) => { if (_boardView.Highlight == square) _boardView.Highlight = null; };
            }
            _handPanel.Controls.Add(text, 0, i + 1);
            _handPanel.Controls.Add(play, 1, i + 1);
            _handPanel.Controls.Add(swap, 2, i + 1);
        }

        // F12: "New hand" instead of "Prospect", with the explanation on screen.
        _newHand.Text = game.CanProspect ? "New hand (1 left)" : "New hand (used)";
        _newHand.Enabled = game.CanProspect;
        var explain = Ui.Label("New hand: throw away all your squares and draw new ones, once per game.");
        explain.MaximumSize = new Size(LogicalToDeviceUnits(230), 0);
        explain.ForeColor = SystemColors.GrayText;
        explain.Anchor = AnchorStyles.Left;
        int last = game.Choices.Count + 1;
        _handPanel.Controls.Add(explain, 0, last);
        _handPanel.Controls.Add(_newHand, 1, last);
        _handPanel.SetColumnSpan(_newHand, 2);
        _handPanel.ResumeLayout();
    }

    private void HumanProspects()
    {
        var game = _game;
        if (game is null || _computerBusy || !game.CanProspect || !game.CurrentPlayer.IsHuman) return;
        if (MessageBox.Show(this, "Throw away your whole hand and draw new squares? You can only do this once per game.",
                "New hand", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        game.Prospect();
        Log($"{game.CurrentPlayer.Name} took a new hand: threw away all their squares and drew new ones.");
        RefreshAll();
    }

    private async Task HumanEndsTurnAsync()
    {
        var game = _game;
        if (game is null || _computerBusy || game.Phase != GamePhase.Trading) return;
        game.EndTurn();
        ReportRoundStart(game);
        await ContinueAsync();
    }

    /// <summary>
    /// Modern (S15): after a merger, each holder of the company taken over chooses how much of their payout to turn into
    /// survivor shares. Humans get a dialog (whoever played the square); computer and Claude players decide for themselves.
    /// </summary>
    private void SettleConversions(Game game, IReadOnlyList<GameEvent> events)
    {
        if (game.PendingConversions.Count == 0) return;
        var requested = new Dictionary<int, BigInteger>();
        foreach (var offer in game.PendingConversions)
        {
            if (_ais[offer.Player] is IAiPlayer ai)
            {
                requested[offer.Player] = ai.ChooseConversion(game, offer);
                continue;
            }
            string? summary = events.OfType<MergerEvent>()
                .Select(m => EventText.MergerSummaryFor(game, m, events, offer.Player, Currency)).LastOrDefault(t => t is not null);
            using var form = new ConversionForm(game, offer, Currency, summary);
            requested[offer.Player] = form.ShowDialog(this) == DialogResult.OK ? form.Shares : 0;
        }
        if (game.SettleConversions(requested) is { Converted.Count: > 0 } done)
            Log(EventText.Describe(game, done, Currency));
        RefreshAll();
    }

    /// <summary>Logs what the round's event card did (Modern). Events never stop play with a dialog.</summary>
    private void ReportRoundStart(Game game)
    {
        // S18: tell a human about their own squares that expired (other players' hands stay private).
        if (game.ExpiredFor >= 0 && game.Players[game.ExpiredFor].IsHuman)
            foreach (var square in game.ExpiredSquares)
                Log($"{game.Players[game.ExpiredFor].Name}: square {square} expired after {ModernRules.SquareExpiryTurns} turns in your hand and was replaced.");
        if (game.RoundStartEvents.Count == 0) return;
        ReportEvents(game, game.RoundStartEvents, popups: false);
        RefreshAll();
    }

    /// <summary>Shows a line of news above the board; it stays for a few seconds and then fades.</summary>
    private void ShowNews(string text)
    {
        _news.Text = text;
        _newsAge = 0;
        _news.ForeColor = Color.Gold;
        Ui.Tip(_news, text);
        _newsTimer.Start();
    }

    private void FadeNews()
    {
        const int hold = 60, fade = 20;   // tenths of a second
        _newsAge++;
        if (_newsAge <= hold) return;
        double t = Math.Min(1, (_newsAge - hold) / (double)fade);
        var from = Color.Gold;
        var to = _news.BackColor;
        _news.ForeColor = Color.FromArgb((int)(from.R + (to.R - from.R) * t), (int)(from.G + (to.G - from.G) * t), (int)(from.B + (to.B - from.B) * t));
        if (t >= 1) _newsTimer.Stop();
    }

    /// <summary>Switches the computer players between a short pause and the speed set in Preferences (S8).</summary>
    private void ToggleFast()
    {
        bool fast = _settings.ComputerDelayMs > FastDelayMs;
        _settings = fast
            ? _settings with { NormalComputerDelayMs = _settings.ComputerDelayMs, ComputerDelayMs = FastDelayMs }
            : _settings with { ComputerDelayMs = _settings.NormalComputerDelayMs ?? 800 };
        _fast.Checked = fast;
        _storage.SaveSettings(_settings);
        Log(fast ? "Computer players are now fast." : $"Computer players are back to a {_settings.ComputerDelayMs} ms pause.");
    }

    private void ReportEvents(Game game, IReadOnlyList<GameEvent> events, bool popups)
    {
        foreach (var e in events)
        {
            Log(EventText.Describe(game, e, Currency));
            // F10: each human affected by a merger gets a summary in their own terms.
            if (e is MergerEvent merger)
                for (int p = 0; p < game.Players.Count; p++)
                    if (game.Players[p].IsHuman && EventText.MergerSummaryFor(game, merger, events, p, Currency) is string summary)
                        Log($"{game.Players[p].Name}: {summary}");
            if (e is CompanyFoundedEvent or MergerEvent or SplitEvent or EventCardEvent { Card: not EventCard.QuietQuarter })
                ShowNews(EventText.Describe(game, e, Currency));
            // The end-of-game warning matters to everyone, so it always pops up. Other pop-ups are optional (S8).
            if ((popups && _settings.ShowComputerPopups && e is CompanyFoundedEvent or MergerEvent or SplitEvent) || e is NoMoreCompaniesEvent)
            {
                RefreshAll();
                Dialogs.ShowEvent(this, game, e, Currency);
            }
        }
    }

    // ------------------------------------------------------------------ companies and players

    /// <summary>
    /// The Advisor (S14): what Hard would do now and why. While choosing, the square is marked on the board; the trades
    /// can be filled into the Market. Each use is counted for the high-score table. "Explain" asks Claude, if set up.
    /// </summary>
    private void ShowAdvice()
    {
        var game = _game;
        if (game is null || _computerBusy || !game.CurrentPlayer.IsHuman || game.Phase == GamePhase.Over) return;
        _advisorUses[game.CurrentPlayerIndex]++;
        var advice = Advisor.Suggest(game, Currency);
        _advisedSquare = advice.Square;
        RefreshAll();
        Log($"Advisor: {(advice.Choice > 0 ? $"play square {advice.Choice}. " : "")}{advice.Reason}");

        var lines = new List<string> { (advice.Choice > 0 ? $"Play square {advice.Choice} (marked on the board). " : "") + advice.Reason };
        bool claude = ClaudeCodeRunner.ResolveExecutable(_settings.Claude.ExecutablePath) is not null;
        using var form = Ui.Dialog("Advisor", 520, 230);
        var text = Ui.Label(string.Join(Environment.NewLine, lines));
        text.MaximumSize = new Size(490, 0);
        text.Padding = new Padding(14, 12, 14, 6);
        text.Dock = DockStyle.Top;
        var ok = Ui.Button("OK");
        ok.DialogResult = DialogResult.OK;
        form.AcceptButton = form.CancelButton = ok;
        var buttons = new List<Control> { ok };
        if (advice.Trades.Count > 0 && game.Phase == GamePhase.Trading)
            buttons.Add(Ui.Button("Fill in the Market", (_, _) => { form.DialogResult = DialogResult.Yes; }, 140));
        if (claude)
            buttons.Add(Ui.Button("Explain…", async (s, _) => await ExplainAdvice(game, advice, text, (Button)s!), 100));
        form.Controls.Add(text);
        form.Controls.Add(Ui.ButtonBar([.. buttons]));
        form.ResumeLayout(false);
        if (form.ShowDialog(this) == DialogResult.Yes) OpenMarket(advice.Trades);
    }

    private const string ExplainSchema = "{\"type\":\"object\",\"properties\":{\"explanation\":{\"type\":\"string\"}},\"required\":[\"explanation\"],\"additionalProperties\":false}";

    /// <summary>Asks Claude for a short plain-English explanation of the Advisor's suggestion.</summary>
    private async Task ExplainAdvice(Game game, Advice advice, Label text, Button button)
    {
        button.Enabled = false;
        text.Text += Environment.NewLine + Environment.NewLine + "Asking Claude (about 10-30 seconds)...";
        try
        {
            var runner = new ClaudeCodeRunner(_settings.Claude);
            var answer = await runner.RunAsync(
                "You explain a board game move to a player in two or three plain sentences. Be concrete about where the money comes from.",
                ClaudeCodeAi.DescribePosition(game) + Environment.NewLine + $"Explain why this is a good move for me: {advice.Reason}",
                ExplainSchema,
                CancellationToken.None);
            text.Text = text.Text.Replace("Asking Claude (about 10-30 seconds)...", "Claude: " + answer.GetProperty("explanation").GetString());
        }
        catch (Exception ex) when (ex is ClaudeCodeException or System.Text.Json.JsonException or KeyNotFoundException)
        {
            text.Text = text.Text.Replace("Asking Claude (about 10-30 seconds)...", "Claude couldn't be reached: " + ex.Message);
        }
    }

    /// <summary>The Market window (S16): every company in one place, with a single Confirm.</summary>
    private void OpenMarket() => OpenMarket(null);

    private void OpenMarket(IReadOnlyList<TradeOrder>? suggested)
    {
        var game = _game;
        if (game is null || _computerBusy || !game.CanTrade || !game.CurrentPlayer.IsHuman) return;
        using var form = new MarketForm(game, Currency, _settings.OpenMarketAfterSquare, trade =>
        {
            Log(EventText.Describe(game, trade, Currency));
            RefreshAll();
        }, suggested, _advisorEnabled ? () => { _advisorUses[game.CurrentPlayerIndex]++; return Advisor.Suggest(game, Currency).Trades; } : null);
        if (_settings.MarketWindow is [var x, var y, var w, var h] && w > 200 && h > 150 &&
            Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(new Rectangle(x, y, w, h))))
        {
            form.StartPosition = FormStartPosition.Manual;
            form.Bounds = new Rectangle(x, y, w, h);
        }
        form.ShowDialog(this);
        var b = form.WindowState == FormWindowState.Normal ? form.Bounds : form.RestoreBounds;
        _settings = _settings with { OpenMarketAfterSquare = form.AutoOpen, MarketWindow = [b.X, b.Y, b.Width, b.Height] };
        _storage.SaveSettings(_settings);
        RefreshAll();
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        if (keyData == Keys.M && _market.Enabled && ActiveForm == this)
        {
            OpenMarket();
            return true;
        }
        if (keyData == Keys.A && _advisor.Visible && _advisor.Enabled && ActiveForm == this)
        {
            ShowAdvice();
            return true;
        }
        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OpenCompany(int company)
    {
        var game = _game;
        if (game is null || !game.Companies[company].Exists) return;
        bool canTrade = !_computerBusy && game.CanTrade && game.CurrentPlayer.IsHuman;
        using var form = new CompanyForm(game, company, Currency, canTrade, trade =>
        {
            Log(EventText.Describe(game, trade, Currency));
            RefreshAll();
        });
        form.ShowDialog(this);
        RefreshAll();
    }

    private void ShowPlayerDetails()
    {
        var game = _game;
        if (game is null || _playerList.SelectedIndices.Count == 0) return;
        int p = (int)_playerList.SelectedItems[0].Tag!;
        if (game.Rules.HideDetails && p != game.CurrentPlayerIndex)
        {
            MessageBox.Show(this, "Player details are hidden due to the preferences set for this game.", "Game Details",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var player = game.Players[p];
        var world = game.WorldFor(p);
        var table = Ui.Table(("Company", 170), ("Shares", 120), ("Worth", 130));
        foreach (var c in world.Companies.Where(c => c.Exists))
            table.Items.Add(Ui.Row(c.Name, Currency.Number(player.Shares[c.Index]), Currency.Format(player.Shares[c.Index] * c.Value)));
        Dialogs.Show(this, $"Details of {player.Name}", SystemColors.Highlight,
            [
                $"{player.Name} ({player.Kind.Label()})",
                $"Money: {Currency.Format(player.Money)}    Shares worth: {Currency.Format(game.ShareWorth(p))}    Net worth: {Currency.Format(game.NetWorth(p))}",
            ], table);
    }

    // ------------------------------------------------------------------ display

    private void Log(string line)
    {
        if (_log.TextLength > 0) _log.AppendText(Environment.NewLine);
        _log.AppendText(line);
    }

    private void SetStatus(string text) => _status.Text = text;

    private IReadOnlyList<Pos>? _previewedChoices;
    private IReadOnlyList<MovePreview> _previews = [];

    /// <summary>What each offered square would do, worked out once per set of choices.</summary>
    private IReadOnlyList<MovePreview> Previews(Game game)
    {
        if (!ReferenceEquals(_previewedChoices, game.Choices))
        {
            _previews = MovePreview.ForChoices(game);
            _previewedChoices = game.Choices;
        }
        return _previews;
    }

    private IReadOnlyList<BoardView.ChoiceMark> ChoiceMarks(Game game) =>
        Previews(game).Select(m => new BoardView.ChoiceMark(
            m.Label,
            m.Company >= 0 ? Color.FromArgb(Company.Colors[m.Company]) : null,
            m.Details(game, Currency))).ToList();

    private void PaintDot(Graphics g, int company, Rectangle r)
    {
        // A white dot means the current player can afford at least one share (MoneyShow in the original).
        var game = _game;
        if (game is null || !InProgress) return;
        var c = game.Companies[company];
        if (!c.Exists || game.CurrentPlayer.Money < c.Value) return;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        int inset = r.Width / 4;
        var dot = Rectangle.Inflate(r, -inset, -inset);
        g.FillEllipse(Brushes.White, dot);
        g.DrawEllipse(Pens.Black, dot);
    }

    private void RefreshAll()
    {
        var game = _game;
        bool inProgress = InProgress;

        _newGame.Enabled = !inProgress;
        _daily.Enabled = !inProgress;
        _endGame.Enabled = inProgress;
        _newGrid.Enabled = !inProgress;
        _gridSize.Enabled = !inProgress;
        _preferences.Enabled = !inProgress;
        foreach (var item in _sizeItems) item.Checked = (GridSize)item.Tag! == _settings.GridSize;

        if (game is null)
        {
            _boardView.ShowBoard(_board, []);
            _goLabel.Text = $"{Spec.Label} grid — {Spec.GoesPerPlayer} goes each, {Spec.Companies} companies";
            _playerLabel.Text = "No game in progress";
            _kindLabel.Text = "";
            _moneyLabel.Text = "";
            _eventLabel.Text = "";
            SetStatus("Choose Game > New Game to start.");
        }
        else
        {
            var player = game.CurrentPlayer;
            // A Modern hand is private, so another player's squares aren't shown.
            bool choosing = inProgress && game.Phase == GamePhase.ChoosingSquare && (!game.IsModern || player.IsHuman);
            if (_advisedSquare is Pos advised && !(choosing && game.Choices.Contains(advised))) _advisedSquare = null;
            _boardView.ShowBoard(game.Board, choosing ? game.Choices : [], _advisedSquare, marks: choosing ? ChoiceMarks(game) : null);
            _goLabel.Text = inProgress
                ? (game.NoMoreCompanies && game.Turn + 1 == game.Rounds ? "Final round" : $"Round {game.Turn + 1} of {game.Rounds}")
                  + (game.Type == GameType.Tournament ? " (tournament)" : "")
                : $"Game over after round {game.Turn}";
            _playerLabel.Text = inProgress ? player.Name : $"Winner: {game.Standings()[0].Name}";
            _kindLabel.Text = inProgress ? player.Kind.Label() : "";
            if (inProgress && game.CurrentPlayerIndex < _personalities.Length && _personalities[game.CurrentPlayerIndex] is var mood and not Personality.None)
                _kindLabel.Text += $" · {mood}: {Personalities.Description(mood)}";
            // A human sees their own secret goal on their turn.
            if (inProgress && player.IsHuman && game.GoalOf(game.CurrentPlayerIndex) is SecretGoal goal)
                _kindLabel.Text += Environment.NewLine + "Your secret goal: " + SecretGoals.Description(goal);
            if (inProgress && player.IsHuman && game.LateBuying)
                _kindLabel.Text += Environment.NewLine + $"Last {ModernRules.LateBuyRounds} rounds: shares you buy now don't count for the final payout ranking.";
            if (inProgress && player.IsHuman && game.IsModern && game.HandOf(game.CurrentPlayerIndex).Count > ModernRules.HandSize)
                _kindLabel.Text += Environment.NewLine + "You're in last place, so you hold an extra square.";
            string Card(EventCard card) => EventCards.Title(card) + (card == EventCard.QuietQuarter ? "" : " — " + game.DescribeEvent(card));
            _eventLabel.Text = !inProgress || game.NextEvent is not EventCard next ? ""
                : game.CurrentEvent is EventCard now
                    ? $"This round: {Card(now)}" + Environment.NewLine + $"Next round: {Card(next)}"
                    : $"Events begin in round {game.NextEventRound}. Round {game.NextEventRound}: {Card(next)}";
            _moneyLabel.Text = inProgress ? Currency.Format(player.Money) : Currency.Format(game.Standings()[0].NetWorth);
            if (inProgress && game.InterestThisTurn > 0) _moneyLabel.Text += $"  (+{Currency.Format(game.InterestThisTurn)} interest)";

            if (!inProgress) SetStatus("Choose Game > New Game to play again.");
            else if (!_computerBusy && player.IsHuman && game.Phase == GamePhase.ChoosingSquare)
                SetStatus(game.IsModern
                    ? "Play a square from your hand below, or click it on the board. Point at a square for details."
                    : "Choose a square (point at one for details):" + Environment.NewLine +
                      string.Join(Environment.NewLine, Previews(game).Select((m, i) => $"{i + 1}. {m.Summary(Currency, game.CurrentPlayerIndex)}" + (m.Allowed is null ? "" : " (not allowed)"))));
            else if (!_computerBusy && player.IsHuman)
                SetStatus("Buy and sell shares by clicking a company, then press End Turn.");
        }

        int slots = game?.Companies.Length ?? Spec.Companies;
        ShowCompanySlots(slots);
        for (int c = 0; c < slots; c++)
        {
            var company = game?.Companies[c];
            bool exists = company?.Exists == true;
            _companyButtons[c].TwoLines = slots > 6;
            string price = exists ? Currency.Format(company!.Value) : "";
            if (exists && game!.IsModern)
                price += game.SharesLeft(c) == 0 ? " · sold out" : $" · {Currency.Number(game.SharesLeft(c))} left";
            _companyButtons[c].SetText(exists ? company!.Name : Company.Names[c], price);
            // S19: how close the company is to its next split.
            Ui.Tip(_companyButtons[c], exists && game!.IsModern ? EventText.SplitProgress(game, c, Currency) : "");
            _companyButtons[c].Enabled = exists && inProgress;
            _companyDots[c].Invalidate();
        }

        _endTurn.Enabled = inProgress && !_computerBusy && game!.Phase == GamePhase.Trading && game.CurrentPlayer.IsHuman;
        ShowHand(game, inProgress);
        bool modern = game?.IsModern == true && inProgress;
        _market.Enabled = inProgress && !_computerBusy && game!.Phase == GamePhase.Trading && game.CurrentPlayer.IsHuman;
        bool advisor = inProgress && _advisorEnabled;
        _advisor.Visible = advisor;
        _advisor.Enabled = advisor && !_computerBusy && game!.CurrentPlayer.IsHuman;
        _turnButtons.ColumnStyles[0].Width = 0;
        _turnButtons.ColumnStyles[1].Width = advisor ? 22 : 0;
        _turnButtons.ColumnStyles[2].Width = 22;
        _turnButtons.ColumnStyles[3].Width = 100 - _turnButtons.ColumnStyles[0].Width - _turnButtons.ColumnStyles[1].Width - 22;

        _playerList.BeginUpdate();
        _playerList.Items.Clear();
        if (game is not null)
        {
            for (int p = 0; p < game.Players.Count; p++)
            {
                var pl = game.Players[p];
                bool hidden = game.Rules.HideDetails && inProgress && p != game.CurrentPlayerIndex;
                string personality = p < _personalities.Length && _personalities[p] != Personality.None ? $" ({_personalities[p]})" : "";
                var row = Ui.Row((p == game.CurrentPlayerIndex && inProgress ? "▶ " : "   ") + pl.Name + personality,
                    hidden ? "hidden" : Currency.Format(game.NetWorth(p)));
                row.Tag = p;
                row.ToolTipText = pl.Kind.Label();
                _playerList.Items.Add(row);
            }
        }
        _playerList.EndUpdate();
        SizePlayersBox(game?.Players.Count ?? 0);
    }

    /// <summary>Makes the players box tall enough to show every player without scrolling.</summary>
    private void SizePlayersBox(int players)
    {
        int rows = Math.Max(players, 3);
        int itemHeight = _playerList.Items.Count > 0 ? _playerList.GetItemRect(0).Height : _playerList.Font.Height + LogicalToDeviceUnits(4);
        int headerHeight = _playerList.Items.Count > 0 ? _playerList.GetItemRect(0).Top : itemHeight + LogicalToDeviceUnits(4);
        int listHeight = headerHeight + rows * itemHeight + (_playerList.Height - _playerList.ClientSize.Height) + LogicalToDeviceUnits(4);
        var box = _playerList.Parent!;
        int height = listHeight + box.DisplayRectangle.Top + box.Padding.Bottom + LogicalToDeviceUnits(2) + box.Margin.Vertical;
        if ((int)_side.RowStyles[3].Height != height) _side.RowStyles[3].Height = height;
        SizeLog();
    }

    /// <summary>Gives the game log the space the other rows leave, but never less than a minimum (then the panel scrolls).</summary>
    private void SizeLog()
    {
        int others = _info.GetPreferredSize(new Size(_side.ClientSize.Width, 0)).Height + _info.Margin.Vertical
                     + (int)_side.RowStyles[1].Height + (int)_side.RowStyles[2].Height + (int)_side.RowStyles[3].Height + _side.Padding.Vertical;
        int height = Math.Max(LogicalToDeviceUnits(140), _side.ClientSize.Height - others - LogicalToDeviceUnits(4));
        if ((int)_side.RowStyles[4].Height != height) _side.RowStyles[4].Height = height;
    }
}
