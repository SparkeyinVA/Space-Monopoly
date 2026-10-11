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
    private List<(string Name, PlayerKind Kind)> _lastPlayers = [("Player 1", PlayerKind.Human), ("Computer", PlayerKind.ComputerMedium)];

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
    private readonly Button _prospect;
    private readonly TableLayoutPanel _turnButtons = new() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Margin = Padding.Empty };
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
        var project = new ToolStripMenuItem("&Game", null, _newGame, _endGame, new ToolStripSeparator(), scores, new ToolStripSeparator(), quit);

        _newGrid = new ToolStripMenuItem("&Create New Grid", null, (_, _) => NewGrid(), Keys.F5);
        _sizeItems = Enum.GetValues<GridSize>()
            .Select(size => new ToolStripMenuItem(GridSpec.For(size).Label, null, (_, _) => SetGridSize(size)) { Tag = size })
            .ToArray();
        _gridSize = new ToolStripMenuItem("&Grid Size", null, _sizeItems);
        _preferences = new ToolStripMenuItem("&Preferences...", null, (_, _) => ShowPreferences());
        var options = new ToolStripMenuItem("&Options", null, _newGrid, _gridSize, new ToolStripSeparator(), _preferences);

        var howTo = new ToolStripMenuItem("&How to Play", null, (_, _) => HelpForms.ShowHowToPlay(this), Keys.F1);
        var about = new ToolStripMenuItem("&About Space Monopoly", null, (_, _) => HelpForms.ShowAbout(this));
        var help = new ToolStripMenuItem("&Help", null, howTo, about);
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
        _prospect = new Button
        {
            Text = "Prospect",
            Dock = DockStyle.Fill,
            Margin = new Padding(4, 8, 4, 8),
            Visible = false,
        };
        _prospect.Click += (_, _) => HumanProspects();
        new ToolTip().SetToolTip(_prospect, "Throw away your hand and draw new squares. You can do this once per game, before you play a square.");
        _turnButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 0));
        _turnButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        _turnButtons.Controls.Add(_prospect, 0, 0);
        _turnButtons.Controls.Add(_endTurn, 1, 0);

        var side = _side;
        side.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        // Fixed rows are given their heights after DPI scaling.
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // info
        side.RowStyles.Add(new RowStyle(SizeType.Absolute));       // companies
        side.RowStyles.Add(new RowStyle(SizeType.Absolute));       // end turn
        side.RowStyles.Add(new RowStyle(SizeType.Absolute));       // players (sized in SizePlayersBox)
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // log

        var info = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        // Long labels wrap instead of widening the side panel past the window edge.
        _goLabel.MaximumSize = _playerLabel.MaximumSize = new Size(330, 0);
        _status.MaximumSize = new Size(300, 0);
        _status.ForeColor = Color.FromArgb(40, 60, 140);
        _eventLabel.ForeColor = Color.FromArgb(150, 60, 0);
        _eventLabel.MaximumSize = new Size(330, 0);
        info.Controls.AddRange([_goLabel, _eventLabel, _playerLabel, _kindLabel, _moneyLabel, _status]);

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

        Controls.Add(_boardView);
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
        using var form = new NewGameForm(_settings.Claude, _lastPlayers, _settings.LastGameType, _settings.LastChaos);
        if (form.ShowDialog(this) != DialogResult.OK || form.Players.Count == 0) return;
        _lastPlayers = form.Players;
        if (form.GameType != _settings.LastGameType || form.Chaos != _settings.LastChaos)
        {
            _settings = _settings with { LastGameType = form.GameType, LastChaos = form.Chaos };
            _storage.SaveSettings(_settings);
        }
        _log.Clear();

        // Modern boards have more planets. Switching between Modern and the 2001 rules draws a new grid.
        if (_boardPlanetOdds != PlanetOddsFor(form.GameType))
        {
            _board = NewBoard(_settings.GridSize);
            Log(form.GameType == GameType.Modern
                ? "A new grid with more planets was drawn for the Modern game."
                : "A new grid with the original number of planets was drawn for this game.");
        }

        // The game starts from the planets on the current grid; any companies left from the last game are cleared.
        _game = new Game(Spec, _board, form.Players, form.GameType, _settings.Rules with { Chaos = form.Chaos });
        _ais = _game.Players.Select(p => p.Kind.IsComputer() ? AiFactory.Create(p.Kind, _settings.Claude) : null).ToArray();
        _cts = new CancellationTokenSource();

        string who = string.Join(", ", _game.Players.Select(p => $"{p.Name} ({p.Kind.Label()})"));
        string goes = $"Each player gets {_game.GoesPerPlayer} goes";
        if (_game.GoesReduced)
            goes += $" (fewer than the usual {_game.MaxGoes}, so that everyone's goes fit on the board)";
        Log($"New {_game.Type.ToString().ToLowerInvariant()} game on the {Spec.Label.ToLowerInvariant()} grid: {who}. " +
            $"{goes}, and there can be up to {Spec.Companies} companies.");
        if (_game.IsModern)
            Log($"Modern rules: you hold a hand of {ModernRules.HandSize} squares (Prospect swaps it once per game). Growth raises share prices but pays " +
                $"no dividends. Buy up to {ModernRules.BuyLimitPerTurn} shares a turn; each company has {ModernRules.SharesPerCompany:N0}. " +
                $"In a merger the two largest holders of the company taken over get {ModernRules.LargestHolderBonus}× and " +
                $"{ModernRules.SecondHolderBonus}× its share price as a bonus. Cash earns {ModernRules.InterestPercent}% interest each turn. " +
                $"Each round an event card is flipped, and the next one is shown in advance ({form.Chaos.ToString().ToLowerInvariant()} events).");
        ReportRoundStart(_game);

        await ContinueAsync();
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
        Dialogs.ShowGameOver(this, game, Currency);

        var fresh = new List<(GridSize Size, bool Modern, int Index)>();
        foreach (var s in game.Standings())
        {
            int index = _highScores.Add(game.Spec.Size, new HighScoreEntry(s.Name, s.NetWorth, s.Kind, game.Type, DateTime.Now));
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
            Log($"{player.Name} prospected: threw away their hand and drew new squares.");

        // Show which square was picked before playing it. A Modern hand stays private: only the chosen square shows.
        if (game.IsModern)
            _boardView.ShowBoard(game.Board, [square], square, ChoiceMarks(game).Where((_, i) => game.Choices[i] == square).ToList());
        else
            _boardView.ShowBoard(game.Board, game.Choices, square, ChoiceMarks(game));
        await Task.Delay(_settings.ComputerDelayMs, token);

        var events = game.PlaceSquare(square);
        if (!string.IsNullOrWhiteSpace(ai.LastComment)) Log($"{player.Name}: \"{ai.LastComment}\"");
        ReportEvents(game, events, popups: _settings.ShowComputerPopups);
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

        // Like the original, open the new company's details so its founder can buy shares straight away.
        if (events.OfType<CompanyFoundedEvent>().LastOrDefault() is { } founded && game.Companies[founded.Company].Exists)
            OpenCompany(founded.Company);

        RefreshAll();
    }

    private void HumanProspects()
    {
        var game = _game;
        if (game is null || _computerBusy || !game.CanProspect || !game.CurrentPlayer.IsHuman) return;
        if (MessageBox.Show(this, "Throw away your hand and draw new squares? You can only do this once per game.",
                "Prospect", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        game.Prospect();
        Log($"{game.CurrentPlayer.Name} prospected: threw away their hand and drew new squares.");
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

    /// <summary>Logs what the round's event card did (Modern). Events never stop play with a dialog.</summary>
    private void ReportRoundStart(Game game)
    {
        if (game.RoundStartEvents.Count == 0) return;
        ReportEvents(game, game.RoundStartEvents, popups: false);
        RefreshAll();
    }

    private void ReportEvents(Game game, IReadOnlyList<GameEvent> events, bool popups)
    {
        foreach (var e in events)
        {
            Log(EventText.Describe(game, e, Currency));
            // The end-of-game warning matters to everyone, so it pops up even for computer players.
            if ((popups && e is CompanyFoundedEvent or MergerEvent or SplitEvent) || e is NoMoreCompaniesEvent)
            {
                RefreshAll();
                Dialogs.ShowEvent(this, game, e, Currency);
            }
        }
    }

    // ------------------------------------------------------------------ companies and players

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
            _boardView.ShowBoard(game.Board, choosing ? game.Choices : [], marks: choosing ? ChoiceMarks(game) : null);
            _goLabel.Text = inProgress
                ? (game.NoMoreCompanies && game.Turn + 1 == game.Rounds ? "Final round" : $"Round {game.Turn + 1} of {game.Rounds}")
                  + (game.Type == GameType.Tournament ? " (tournament)" : "")
                : $"Game over after round {game.Turn}";
            _playerLabel.Text = inProgress ? player.Name : $"Winner: {game.Standings()[0].Name}";
            _kindLabel.Text = inProgress ? player.Kind.Label() : "";
            _eventLabel.Text = inProgress && game.CurrentEvent is EventCard now && game.NextEvent is EventCard next
                ? $"This round: {EventCards.Title(now)}" + (now == EventCard.QuietQuarter ? "" : $" — {EventCards.Description(now)}") +
                  Environment.NewLine + $"Next round: {EventCards.Title(next)}" + (next == EventCard.QuietQuarter ? "" : $" — {EventCards.Description(next)}")
                : "";
            _moneyLabel.Text = inProgress ? Currency.Format(player.Money) : Currency.Format(game.Standings()[0].NetWorth);
            if (inProgress && game.InterestThisTurn > 0) _moneyLabel.Text += $"  (+{Currency.Format(game.InterestThisTurn)} interest)";

            if (!inProgress) SetStatus("Choose Game > New Game to play again.");
            else if (!_computerBusy && player.IsHuman && game.Phase == GamePhase.ChoosingSquare)
                SetStatus((game.IsModern ? "Play a square from your hand" + (game.CanProspect ? ", or Prospect for a new hand" : "") : "Choose a square") +
                          " (point at one for details):" + Environment.NewLine +
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
            if (exists && game!.IsModern) price += $" · {Currency.Number(game.SharesLeft(c))} left";
            _companyButtons[c].SetText(exists ? company!.Name : Company.Names[c], price);
            _companyButtons[c].Enabled = exists && inProgress;
            _companyDots[c].Invalidate();
        }

        _endTurn.Enabled = inProgress && !_computerBusy && game!.Phase == GamePhase.Trading && game.CurrentPlayer.IsHuman;
        bool modern = game?.IsModern == true && inProgress;
        _prospect.Visible = modern;
        _prospect.Enabled = modern && !_computerBusy && game!.CurrentPlayer.IsHuman && game.CanProspect;
        _turnButtons.ColumnStyles[0].Width = modern ? 35 : 0;
        _turnButtons.ColumnStyles[1].Width = modern ? 65 : 100;

        _playerList.BeginUpdate();
        _playerList.Items.Clear();
        if (game is not null)
        {
            for (int p = 0; p < game.Players.Count; p++)
            {
                var pl = game.Players[p];
                bool hidden = game.Rules.HideDetails && inProgress && p != game.CurrentPlayerIndex;
                var row = Ui.Row((p == game.CurrentPlayerIndex && inProgress ? "▶ " : "   ") + pl.Name,
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
    }
}
