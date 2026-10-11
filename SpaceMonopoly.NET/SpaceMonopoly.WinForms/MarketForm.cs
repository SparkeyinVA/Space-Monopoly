using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>
/// One window for trading every company (S16): prices, holdings and majorities side by side, amounts for several
/// companies, and a single Confirm. Sells are applied before buys; problems are shown beside the row.
/// </summary>
internal sealed class MarketForm : Form
{
    private const int ColLetter = 0, ColName = 1, ColPrice = 2, ColSize = 3, ColLeft = 4, ColYou = 5, ColHolders = 6,
        ColBuy = 7, ColSell = 8, ColMax = 9, ColSellAll = 10, ColNote = 11, ColSplit = 12;

    private readonly Game _game;
    private readonly Currency _currency;
    private readonly Action<TradeEvent> _onTrade;
    private readonly MarketOrder _order = new();
    private readonly DataGridView _grid = new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AllowUserToResizeRows = false,
        RowHeadersVisible = false,
        SelectionMode = DataGridViewSelectionMode.CellSelect,
        AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
        BackgroundColor = SystemColors.Window,
        BorderStyle = BorderStyle.None,
        EditMode = DataGridViewEditMode.EditOnEnter,
    };
    private readonly Label _header = Ui.Label("", 10f, FontStyle.Bold);
    private readonly Label _event = Ui.Label("");
    private readonly Label _summary = Ui.Label("");
    private readonly Button _confirm;
    private readonly CheckBox _autoOpen = new() { Text = "Open the Market after I choose a square", AutoSize = true };
    private readonly List<int> _rowCompany = [];

    public MarketForm(Game game, Currency currency, bool autoOpen, Action<TradeEvent> onTrade,
        IReadOnlyList<SpaceMonopoly.Core.Ai.TradeOrder>? suggested = null, Func<IReadOnlyList<SpaceMonopoly.Core.Ai.TradeOrder>>? advisor = null)
    {
        _game = game;
        _currency = currency;
        _onTrade = onTrade;

        SuspendLayout();
        Text = "Market";
        StartPosition = FormStartPosition.CenterParent;
        Ui.KeepOnScreen(this);   // F17: never off the top of a second monitor
        ShowInTaskbar = false;
        MinimizeBox = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(1000, 470);
        MinimumSize = new Size(760, 320);
        KeyPreview = true;

        BuildColumns();
        // F16: the grid's editing box isn't scaled for the screen like the rest of the window, so typed amounts
        // came out tiny. Give the cells and the editing box the window's (scaled) font explicitly.
        _grid.Font = Font;
        _grid.DefaultCellStyle.Font = Font;
        _grid.ColumnHeadersDefaultCellStyle.Font = Font;
        _grid.EditingControlShowing += (_, e) => e.Control.Font = e.CellStyle.Font ?? Font;
        _grid.CellEndEdit += (_, _) => ReadAmounts();
        _grid.CellContentClick += (_, e) => RowButton(e.RowIndex, e.ColumnIndex);
        _grid.DataError += (_, e) => e.ThrowException = false;
        _grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_grid.IsCurrentCellDirty) _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        };
        _grid.CellValueChanged += (_, e) => { if (e.ColumnIndex is ColBuy or ColSell) ReadAmounts(); };

        var top = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Padding = new Padding(10, 8, 10, 4) };
        _event.ForeColor = Color.FromArgb(150, 60, 0);
        top.Controls.AddRange([_header, _event]);

        _summary.Dock = DockStyle.Bottom;
        _summary.AutoSize = false;
        _summary.Height = 44;
        _summary.Padding = new Padding(10, 4, 10, 4);

        _autoOpen.Checked = autoOpen;
        var clear = Ui.Button("Clear", (_, _) => { _order.Clear(); Refill(); });
        var advise = Ui.Button("Advisor", (_, _) => { if (advisor is not null) Fill(advisor()); });
        advise.Visible = advisor is not null;
        Ui.Tip(advise, "Fill in the trades the Hard computer player would make now (counted in the high scores).");
        _confirm = Ui.Button("Confirm trades", (_, _) => Confirm(), 130);
        var close = Ui.Button("Close");
        close.DialogResult = DialogResult.Cancel;
        CancelButton = close;
        var bar = Ui.ButtonBar(close, _confirm, clear, advise);
        bar.Controls.Add(_autoOpen);
        _autoOpen.Margin = new Padding(12, 8, 12, 0);

        Controls.Add(_grid);
        Controls.Add(top);
        Controls.Add(_summary);
        Controls.Add(bar);
        ResumeLayout(false);
        PerformLayout();
        Refill();
        if (suggested is not null) Fill(suggested);

        // F18: open tall enough to show every company without scrolling, at this screen's scaling (or the remembered
        // size if that's bigger). KeepOnScreen then caps it to the working area.
        Load += (_, _) =>
        {
            int needed = _grid.ColumnHeadersHeight + _grid.Rows.GetRowsHeight(DataGridViewElementStates.Visible) + LogicalToDeviceUnits(6);
            int grow = needed - _grid.ClientSize.Height;
            if (grow > 0) Height += grow;
        };
    }

    /// <summary>Puts suggested trades into the order (quantity 0 means as many as possible), for the player to accept or change.</summary>
    private void Fill(IReadOnlyList<SpaceMonopoly.Core.Ai.TradeOrder> trades)
    {
        _order.Clear();
        foreach (var t in trades.Where(t => t.Action == SpaceMonopoly.Core.Ai.TradeAction.Sell))
            _order.Sells[t.Company] = t.Quantity == 0 ? _game.CurrentPlayer.Shares[t.Company] : t.Quantity;
        foreach (var t in trades.Where(t => t.Action == SpaceMonopoly.Core.Ai.TradeAction.Buy))
            _order.Buys[t.Company] = _order.BuyOf(t.Company) + (t.Quantity == 0 ? _order.MaxBuy(_game, t.Company) : t.Quantity);
        Refill();
    }

    /// <summary>Whether the player wants the Market to open by itself after choosing a square.</summary>
    public bool AutoOpen => _autoOpen.Checked;

    private void BuildColumns()
    {
        DataGridViewTextBoxColumn Text(string header, int weight, bool readOnly = true, DataGridViewContentAlignment align = DataGridViewContentAlignment.MiddleLeft) =>
            new() { HeaderText = header, FillWeight = weight, ReadOnly = readOnly, DefaultCellStyle = { Alignment = align }, SortMode = DataGridViewColumnSortMode.Automatic };
        DataGridViewButtonColumn Button(string text, int weight) =>
            new() { HeaderText = "", Text = text, UseColumnTextForButtonValue = true, FillWeight = weight };

        var right = DataGridViewContentAlignment.MiddleRight;
        _grid.Columns.AddRange(
            Text("Co", 22, align: DataGridViewContentAlignment.MiddleCenter),
            Text("Company", 95),
            Text("Price", 55, align: right),
            Text("Size", 34, align: right),
            Text("Left", 40, align: right),
            Text("You", 42, align: right),
            Text("Biggest holders", 140),
            Text("Buy", 42, readOnly: false, align: right),
            Text("Sell", 42, readOnly: false, align: right),
            Button("Max", 38),
            Button("Sell all", 46),
            Text("", 150),
            Text("Next split", 80));
        _grid.Columns[ColBuy].DefaultCellStyle.BackColor = Color.FromArgb(240, 255, 240);
        _grid.Columns[ColSell].DefaultCellStyle.BackColor = Color.FromArgb(255, 242, 242);
        _grid.Columns[ColNote].DefaultCellStyle.ForeColor = Color.Firebrick;
        _grid.Columns[ColLeft].Visible = _game.IsModern;
        _grid.Columns[ColSplit].Visible = _game.IsModern;
    }

    private void Refill()
    {
        var me = _game.CurrentPlayer;
        int self = _game.CurrentPlayerIndex;
        bool hidden = _game.Rules.HideDetails;
        _rowCompany.Clear();
        _grid.Rows.Clear();

        // Squares in your hand that touch each company, with what they'd do (Modern).
        var hints = new Dictionary<int, List<string>>();
        if (_game.IsModern)
            foreach (var square in _game.HandOf(self))
            {
                var preview = MovePreview.For(_game, square);
                foreach (int c in _game.Board.Survey(square).Companies)
                    (hints.TryGetValue(c, out var list) ? list : hints[c] = []).Add($"Square {square} in your hand: {preview.Summary(_currency, self)}");
            }

        foreach (var company in _game.Companies.Where(c => c.Exists))
        {
            int c = company.Index;
            var holders = Enumerable.Range(0, _game.Players.Count)
                .Where(p => _game.Players[p].Shares[c] > 0 && (!hidden || p == self))
                .OrderByDescending(p => _game.Players[p].Shares[c]).ToList();
            int rank = holders.IndexOf(self) + 1;
            string Ordinal(int r) => r switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{r}th" };
            string Holder(int p) => p == self
                ? $"You {Currency.Number(me.Shares[c])}" + (hidden ? "" : $" ({Ordinal(rank)})")
                : $"{_game.Players[p].Name} {Currency.Number(_game.Players[p].Shares[c])}";
            string top = string.Join(" · ", holders.Take(2).Select(Holder));
            if (rank > 2) top += " · " + Holder(self);
            if (_game.IsModern && me.Shares[c] > 0 && me.Shares[c] < ModernRules.MinSharesForBonus) top += $" ⚠ under {ModernRules.MinSharesForBonus}";

            int row = _grid.Rows.Add(
                Company.LetterFor(c).ToString(), company.Name, _currency.Format(company.Value), _game.Board.CompanySize(c),
                _game.IsModern ? Currency.Number(_game.SharesLeft(c)) : "", Currency.Number(me.Shares[c]), top,
                _order.BuyOf(c) == 0 ? "" : _order.BuyOf(c).ToString(), _order.SellOf(c) == 0 ? "" : _order.SellOf(c).ToString(), null, null, "",
                ModernRules.NextSplit(company.Splits) is var (sq, at) ? $"{sq} sq & {_currency.Format(at)}" : "blue chip");
            _rowCompany.Add(c);
            var cells = _grid.Rows[row].Cells;
            cells[ColLetter].Style.BackColor = Color.FromArgb(Company.Colors[c]);
            cells[ColLetter].Style.ForeColor = Company.UsesDarkText(c) ? Color.Black : Color.White;
            if (hints.TryGetValue(c, out var hintList))
            {
                cells[ColName].Style.BackColor = Color.LightYellow;
                cells[ColName].ToolTipText = string.Join(Environment.NewLine, hintList);
            }
        }
        UpdateSummary();
    }

    private void ReadAmounts()
    {
        for (int row = 0; row < _grid.Rows.Count; row++)
        {
            int c = _rowCompany[row];
            _order.Buys[c] = Parse(_grid.Rows[row].Cells[ColBuy].Value);
            _order.Sells[c] = Parse(_grid.Rows[row].Cells[ColSell].Value);
        }
        UpdateSummary();
    }

    private static BigInteger Parse(object? value) =>
        BigInteger.TryParse(Convert.ToString(value)?.Replace(",", "").Trim(), out var n) && n > 0 ? n : 0;

    private void RowButton(int row, int column)
    {
        if (row < 0 || column is not (ColMax or ColSellAll)) return;
        int c = _rowCompany[row];
        if (column == ColMax)
        {
            _order.Buys[c] = 0;
            _order.Buys[c] = _order.MaxBuy(_game, c);
        }
        else
        {
            _order.Sells[c] = _game.CurrentPlayer.Shares[c];
        }
        var cells = _grid.Rows[row].Cells;
        cells[ColBuy].Value = _order.BuyOf(c) == 0 ? "" : _order.BuyOf(c).ToString();
        cells[ColSell].Value = _order.SellOf(c) == 0 ? "" : _order.SellOf(c).ToString();
        UpdateSummary();
    }

    private void UpdateSummary()
    {
        var me = _game.CurrentPlayer;
        var allowance = _game.BuyAllowanceLeft;
        _header.Text = $"Market — Round {_game.Turn + 1} of {_game.Rounds} · Cash {_currency.Format(me.Money)}" +
                       (allowance >= 0 ? $" · You can buy {allowance} more shares this turn" : "");
        _event.Text = _game.NextEvent is EventCard next && next != EventCard.QuietQuarter
            ? $"Round {_game.NextEventRound}: {EventCards.Title(next)} — {_game.DescribeEvent(next)}"
            : "";

        var check = _order.Check(_game);
        for (int row = 0; row < _grid.Rows.Count; row++)
        {
            var note = _grid.Rows[row].Cells[ColNote];
            note.Value = check.Problems.GetValueOrDefault(_rowCompany[row], "");
            note.ToolTipText = (string)note.Value;
        }

        string Describe(Dictionary<int, BigInteger> side) => string.Join(", ", side.Where(x => x.Value > 0)
            .Select(x => $"{Currency.Number(x.Value)} {Company.Names[x.Key]}"));
        string buys = Describe(_order.Buys), sells = Describe(_order.Sells);
        _summary.Text = _order.IsEmpty
            ? "Type amounts in the Buy and Sell columns (or use Max / Sell all), then Confirm trades. Sales happen first, so they can pay for purchases."
            : $"This order: {(sells.Length > 0 ? $"sell {sells} ({_currency.Format(check.Proceeds)})" : "no sales")} · " +
              $"{(buys.Length > 0 ? $"buy {buys} ({_currency.Format(check.Cost)})" : "no purchases")}{Environment.NewLine}" +
              $"Cash after: {_currency.Format(check.CashAfter)}" + (check.AllowanceAfter >= 0 ? $" · Buying allowance after: {check.AllowanceAfter}" : "");
        _confirm.Enabled = !_order.IsEmpty && check.Ok;
    }

    private void Confirm()
    {
        _grid.EndEdit();
        ReadAmounts();
        if (!_order.Check(_game).Ok) return;
        foreach (var trade in _order.Execute(_game)) _onTrade(trade);
        Refill();
    }
}
