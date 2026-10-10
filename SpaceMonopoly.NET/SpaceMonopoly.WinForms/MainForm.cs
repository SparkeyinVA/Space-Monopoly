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
    private readonly Label _status = Ui.Label("");
    private readonly Button[] _companyButtons = new Button[Company.MaxCount];
    private readonly Panel[] _companyDots = new Panel[Company.MaxCount];
    private readonly TableLayoutPanel _companyGrid = new() { Dock = DockStyle.Fill, AutoSize = true };
    private int _companySlots = -1;
    private readonly Button _endTurn;
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
        ClientSize = new Size(1100, 760);
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
            Dock = DockStyle.Top,
            Height = 40,
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Margin = new Padding(4, 8, 4, 8),
        };
        _endTurn.Click += async (_, _) => await HumanEndsTurnAsync();

        var side = new TableLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 360,
            ColumnCount = 1,
            Padding = new Padding(8),
            BackColor = SystemColors.Control,
        };
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // info
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // companies
        side.RowStyles.Add(new RowStyle(SizeType.AutoSize));       // end turn
        side.RowStyles.Add(new RowStyle(SizeType.Absolute, 120));  // players
        side.RowStyles.Add(new RowStyle(SizeType.Percent, 100));   // log

        var info = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Dock = DockStyle.Fill, WrapContents = false };
        _status.MaximumSize = new Size(300, 0);
        _status.ForeColor = Color.FromArgb(40, 60, 140);
        info.Controls.AddRange([_goLabel, _playerLabel, _kindLabel, _moneyLabel, _status]);

        var companies = new GroupBox { Text = "Companies (click for details and trading)", Dock = DockStyle.Fill, AutoSize = true, Padding = new Padding(6) };
        for (int c = 0; c < Company.MaxCount; c++)
        {
            int index = c;
            // Sized in ShowCompanySlots: these are added after the form has been scaled for the screen's DPI.
            var dot = new Panel { Anchor = AnchorStyles.None, Margin = Padding.Empty, BackColor = Color.FromArgb(Company.Colors[c]) };
            dot.Paint += (_, e) => PaintDot(e.Graphics, index, dot.ClientRectangle);
            var button = new Button
            {
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Margin = new Padding(2),
            };
            button.Click += (_, _) => OpenCompany(index);
            _companyDots[c] = dot;
            _companyButtons[c] = button;
        }
        companies.Controls.Add(_companyGrid);

        _playerList.DoubleClick += (_, _) => ShowPlayerDetails();
        var playersBox = new GroupBox { Text = "Players (double-click for details)", Dock = DockStyle.Fill, Padding = new Padding(6) };
        playersBox.Controls.Add(_playerList);

        var logBox = new GroupBox { Text = "Game log", Dock = DockStyle.Fill, Padding = new Padding(6) };
        logBox.Controls.Add(_log);

        side.Controls.Add(info, 0, 0);
        side.Controls.Add(companies, 0, 1);
        side.Controls.Add(_endTurn, 0, 2);
        side.Controls.Add(playersBox, 0, 3);
        side.Controls.Add(logBox, 0, 4);

        _boardView.SquareClicked += (_, p) => HumanChooses(p);

        Controls.Add(_boardView);
        Controls.Add(side);
        Controls.Add(menu);
        ResumeLayout(false);
        PerformLayout();

        _board = NewBoard(_settings.GridSize);
        RefreshAll();
        Log("Welcome to Space Monopoly! Choose Game > New Game to start. " +
            "Use Options > Create New Grid until you like the look of the board — lots of planets close together is best.");
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
        for (int c = 0; c < count; c++)
        {
            _companyButtons[c].Height = LogicalToDeviceUnits(twoColumns ? 42 : 32);
            _companyDots[c].Size = LogicalToDeviceUnits(new Size(20, 20));
            _companyGrid.Controls.Add(_companyDots[c], c / perColumn * 2, c % perColumn);
            _companyGrid.Controls.Add(_companyButtons[c], c / perColumn * 2 + 1, c % perColumn);
        }
        _companyGrid.ResumeLayout(true);
    }
    private GridSpec Spec => GridSpec.For(_settings.GridSize);
    private bool InProgress => _game is { Phase: not GamePhase.Over };

    // ------------------------------------------------------------------ board and menus

    private Board NewBoard(GridSize size)
    {
        var spec = GridSpec.For(size);
        var board = new Board(spec.Width, spec.Height);
        board.Randomise(_rng);
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

    private void ShowHighScores(IReadOnlyCollection<(GridSize, int)> fresh)
    {
        using var form = new HighScoresForm(_highScores, Currency, _settings.GridSize, fresh);
        form.ShowDialog(this);
    }

    // ------------------------------------------------------------------ starting and ending games

    private async void NewGame()
    {
        if (InProgress) return;
        using var form = new NewGameForm(_settings.Claude, _lastPlayers);
        if (form.ShowDialog(this) != DialogResult.OK || form.Players.Count == 0) return;
        _lastPlayers = form.Players;

        // The game starts from the planets on the current grid; any companies left from the last game are cleared.
        _game = new Game(Spec, _board, form.Players, form.GameType, _settings.Rules);
        _ais = _game.Players.Select(p => p.Kind.IsComputer() ? AiFactory.Create(p.Kind, _settings.Claude) : null).ToArray();
        _cts = new CancellationTokenSource();
        _log.Clear();

        string who = string.Join(", ", _game.Players.Select(p => $"{p.Name} ({p.Kind.Label()})"));
        string goes = $"Each player gets {_game.GoesPerPlayer} goes";
        if (_game.GoesReduced)
            goes += $" (fewer than the usual {Spec.GoesPerPlayer}, so that everyone's goes fit on the board)";
        Log($"New {_game.Type.ToString().ToLowerInvariant()} game on the {Spec.Label.ToLowerInvariant()} grid: {who}. " +
            $"{goes}, and there can be up to {Spec.Companies} companies.");

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

        var fresh = new List<(GridSize, int)>();
        foreach (var s in game.Standings())
        {
            int index = _highScores.Add(game.Spec.Size, new HighScoreEntry(s.Name, s.NetWorth, s.Kind, game.Type, DateTime.Now));
            if (index >= 0)
            {
                // Earlier new entries move down when a later one is inserted above them.
                for (int i = 0; i < fresh.Count; i++)
                    if (fresh[i].Item2 >= index) fresh[i] = (fresh[i].Item1, fresh[i].Item2 + 1);
                fresh.Add((game.Spec.Size, index));
            }
        }
        fresh.RemoveAll(f => f.Item2 >= HighScoreTable.MaxEntries);
        _storage.SaveHighScores(_highScores);
        ShowHighScores(fresh);
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
        var square = await ai.ChooseSquareAsync(game, token);
        token.ThrowIfCancellationRequested();

        // Show which square was picked before playing it.
        _boardView.ShowBoard(game.Board, game.Choices, square);
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

    private async Task HumanEndsTurnAsync()
    {
        var game = _game;
        if (game is null || _computerBusy || game.Phase != GamePhase.Trading) return;
        game.EndTurn();
        await ContinueAsync();
    }

    private void ReportEvents(Game game, IReadOnlyList<GameEvent> events, bool popups)
    {
        foreach (var e in events)
        {
            Log(EventText.Describe(game, e, Currency));
            if (popups && e is CompanyFoundedEvent or MergerEvent or SplitEvent)
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
            SetStatus("Choose Game > New Game to start.");
        }
        else
        {
            var player = game.CurrentPlayer;
            _boardView.ShowBoard(game.Board, inProgress && game.Phase == GamePhase.ChoosingSquare ? game.Choices : []);
            _goLabel.Text = inProgress
                ? $"Round {game.Turn + 1} of {game.GoesPerPlayer}" + (game.Type == GameType.Tournament ? " (tournament)" : "")
                : $"Game over after round {game.Turn}";
            _playerLabel.Text = inProgress ? player.Name : $"Winner: {game.Standings()[0].Name}";
            _kindLabel.Text = inProgress ? player.Kind.Label() : "";
            _moneyLabel.Text = inProgress ? Currency.Format(player.Money) : Currency.Format(game.Standings()[0].NetWorth);

            if (!inProgress) SetStatus("Choose Game > New Game to play again.");
            else if (!_computerBusy && player.IsHuman && game.Phase == GamePhase.ChoosingSquare)
                SetStatus("Choose one of the flashing squares on the board.");
            else if (!_computerBusy && player.IsHuman)
                SetStatus("Buy and sell shares by clicking a company, then press End Turn.");
        }

        int slots = game?.Companies.Length ?? Spec.Companies;
        ShowCompanySlots(slots);
        string gap = slots > 6 ? Environment.NewLine : "   ";
        for (int c = 0; c < slots; c++)
        {
            var company = game?.Companies[c];
            bool exists = company?.Exists == true;
            _companyButtons[c].Text = exists ? $"{company!.Name}{gap}{Currency.Format(company.Value)}" : Company.Names[c];
            _companyButtons[c].Enabled = exists && inProgress;
            _companyDots[c].Invalidate();
        }

        _endTurn.Enabled = inProgress && !_computerBusy && game!.Phase == GamePhase.Trading && game.CurrentPlayer.IsHuman;

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
    }
}
