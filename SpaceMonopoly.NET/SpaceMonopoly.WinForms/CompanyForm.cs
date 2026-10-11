using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>Company information and share trading (ComInfoForm in the original).</summary>
internal sealed class CompanyForm : Form
{
    private readonly Game _game;
    private readonly Currency _currency;
    private readonly bool _canTrade;
    private readonly Action<TradeEvent> _onTrade;
    private int _company;

    private readonly Panel _header = new() { Dock = DockStyle.Top, Height = 54 };
    private readonly Label _title = new() { Dock = DockStyle.Fill, TextAlign = ContentAlignment.MiddleCenter, Font = new Font("Segoe UI", 15f, FontStyle.Bold) };
    private readonly Label _price = Ui.Label("", 10f, FontStyle.Bold);
    private readonly Label _size = Ui.Label("");
    private readonly Label _major = Ui.Label("");
    private readonly Label _owned = Ui.Label("");
    private readonly Label _canBuy = Ui.Label("");
    private readonly NumericUpDown _buyQty = new() { Minimum = 0, Maximum = decimal.MaxValue, Width = 150, ThousandsSeparator = true };
    private readonly NumericUpDown _sellQty = new() { Minimum = 0, Maximum = decimal.MaxValue, Width = 150, ThousandsSeparator = true };
    private readonly Button _buy, _maxBuy, _sell, _sellAll;

    public CompanyForm(Game game, int company, Currency currency, bool canTrade, Action<TradeEvent> onTrade)
    {
        _game = game;
        _company = company;
        _currency = currency;
        _canTrade = canTrade;
        _onTrade = onTrade;

        SuspendLayout();
        Text = "Company Information";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(470, 370);

        _header.Controls.Add(_title);

        var info = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 2, Padding = new Padding(12, 8, 12, 8) };
        info.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        info.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        info.Controls.Add(Ui.Label("Share price:"), 0, 0); info.Controls.Add(_price, 1, 0);
        info.Controls.Add(Ui.Label("Company size:"), 0, 1); info.Controls.Add(_size, 1, 1);
        info.Controls.Add(Ui.Label("Major shareholder:"), 0, 2); info.Controls.Add(_major, 1, 2);

        _owned.Dock = DockStyle.Top;
        _owned.TextAlign = ContentAlignment.MiddleCenter;
        _owned.AutoSize = false;
        _owned.Height = 24;
        _canBuy.Dock = DockStyle.Top;
        _canBuy.TextAlign = ContentAlignment.MiddleCenter;
        _canBuy.AutoSize = false;
        _canBuy.Height = 44;   // two lines in a Modern game

        _buy = Ui.Button("Buy >>>", (_, _) => Trade(isBuy: true, new BigInteger(_buyQty.Value)));
        _maxBuy = Ui.Button("Max Buy", (_, _) => Trade(isBuy: true, _game.MaxBuy(_company)));
        _sell = Ui.Button("Sell >>>", (_, _) => Trade(isBuy: false, new BigInteger(_sellQty.Value)));
        _sellAll = Ui.Button("Sell All", (_, _) => Trade(isBuy: false, _game.CurrentPlayer.Shares[_company]));

        var trade = new TableLayoutPanel { Dock = DockStyle.Top, AutoSize = true, ColumnCount = 4, Padding = new Padding(12, 4, 12, 4) };
        trade.Controls.Add(Ui.Label("Buy shares:"), 0, 0); trade.Controls.Add(_buyQty, 1, 0); trade.Controls.Add(_buy, 2, 0); trade.Controls.Add(_maxBuy, 3, 0);
        trade.Controls.Add(Ui.Label("Sell shares:"), 0, 1); trade.Controls.Add(_sellQty, 1, 1); trade.Controls.Add(_sell, 2, 1); trade.Controls.Add(_sellAll, 3, 1);
        foreach (Control c in trade.Controls) c.Anchor = AnchorStyles.Left;

        var tradeHint = Ui.Label(canTrade ? "" : "Shares can be bought and sold after you've chosen a square on your turn.");
        tradeHint.Dock = DockStyle.Top;
        tradeHint.ForeColor = SystemColors.GrayText;
        tradeHint.Padding = new Padding(12, 0, 12, 0);

        var prev = Ui.Button("<< Previous", (_, _) => Step(-1));
        var next = Ui.Button("Next >>", (_, _) => Step(+1));
        var close = Ui.Button("Close");
        close.DialogResult = DialogResult.OK;
        CancelButton = close;

        Controls.Add(tradeHint);
        Controls.Add(trade);
        Controls.Add(_canBuy);
        Controls.Add(_owned);
        Controls.Add(info);
        Controls.Add(_header);
        Controls.Add(Ui.ButtonBar(close, next, prev));

        _buyQty.Enter += (_, _) => AcceptButton = _buy;
        _sellQty.Enter += (_, _) => AcceptButton = _sell;

        ShowCompany(company);
        ResumeLayout(false);
        PerformLayout();
    }

    private void Step(int delta)
    {
        int c = _company;
        int count = _game.Companies.Length;
        for (int i = 0; i < count; i++)
        {
            c = (c + delta + count) % count;
            if (_game.Companies[c].Exists)
            {
                ShowCompany(c);
                return;
            }
        }
    }

    private void Trade(bool isBuy, BigInteger quantity)
    {
        if (_game.ValidateTrade(_company, quantity, isBuy) is string error)
        {
            MessageBox.Show(this, error, "I don't think so...", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            return;
        }
        var trade = isBuy ? _game.Buy(_company, quantity) : _game.Sell(_company, quantity);
        _onTrade(trade);
        ShowCompany(_company);
    }

    private void ShowCompany(int company)
    {
        _company = company;
        var c = _game.Companies[company];
        var me = _game.CurrentPlayer;

        _header.BackColor = Color.FromArgb(c.Argb);
        _title.ForeColor = Company.UsesDarkText(company) ? Color.Black : Color.White;
        _title.Text = c.Name;
        _price.Text = _currency.Format(c.Value);
        int size = _game.Board.CompanySize(company);
        _size.Text = $"{size} square{(size == 1 ? "" : "s")}";

        _major.Text = _game.MajorShareholder(company) is var (player, shares)
            ? $"{_game.Players[player].Name} with {Currency.Number(shares)} share{(shares == 1 ? "" : "s")}."
            : "Nobody.";

        var owned = me.Shares[company];
        var max = _game.MaxBuy(company);
        _owned.Text = $"{me.Name} owns {Currency.Number(owned)} share{(owned == 1 ? "" : "s")} in this company.";
        _canBuy.Text = $"{me.Name} has {_currency.Format(me.Money)} and can buy {(max > 1 ? "up to " : "")}{Currency.Number(max)} share{(max == 1 ? "" : "s")}.";
        if (_game.IsModern)
            _canBuy.Text += Environment.NewLine + $"Buying left this turn: {_game.BuyAllowanceLeft} of {ModernRules.BuyLimitPerTurn}. " +
                            $"Shares left in the company: {Currency.Number(_game.SharesLeft(company))}.";

        _buyQty.Value = 0;
        _sellQty.Value = 0;
        bool trading = _canTrade && _game.CanTrade;
        _buy.Enabled = _maxBuy.Enabled = _buyQty.Enabled = trading && max > 0;
        _sell.Enabled = _sellAll.Enabled = _sellQty.Enabled = trading && owned > 0;
    }
}
