using SpaceMonopoly.Core;

namespace SpaceMonopoly.WinForms;

/// <summary>
/// Modern (S15): a human holder of a company that was just taken over chooses how much of the money they were paid
/// to turn into shares of the surviving company, at its new price.
/// </summary>
internal sealed class ConversionForm : Form
{
    private readonly NumericUpDown _shares = new() { Minimum = 0, Width = 110 };
    private readonly Label _summary = Ui.Label("");

    public ConversionForm(Game game, ConversionOffer offer, Currency currency, string? summary = null)
    {
        var survivor = game.Companies[offer.Survivor];
        long price = survivor.Value;
        var most = offer.MostShares(price);
        var left = game.SharesLeft(offer.Survivor);
        if (left >= 0) most = BigInteger.Min(most, left);
        most = BigInteger.Min(most, game.Players[offer.Player].Money / Math.Max(1, price));

        SuspendLayout();
        Text = $"Merger: {game.Players[offer.Player].Name}";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Ui.KeepOnScreen(this);   // F17: never off the top of a second monitor
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(470, summary is null ? 230 : 290);

        var intro = Ui.Label((summary is null ? "" : summary + Environment.NewLine + Environment.NewLine) +
                             $"{game.Players[offer.Player].Name}, in all you were paid {currency.Format(offer.Owed)} for your shares in the company taken over " +
                             $"(including any majority bonus).{Environment.NewLine}{Environment.NewLine}" +
                             $"You can turn some or all of it into {survivor.Name} shares at their new price of {currency.Format(price)}. " +
                             "This doesn't use up your buying allowance. Whatever you don't convert stays as cash.");
        intro.MaximumSize = new Size(440, 0);
        intro.Padding = new Padding(12, 12, 12, 6);
        intro.Dock = DockStyle.Top;

        _shares.Maximum = (decimal)most;
        _shares.Font = Font;   // F16: keep the amount box the same size as the rest of the dialog
        _shares.ValueChanged += (_, _) => UpdateSummary(currency, price);
        var max = Ui.Button("Max", (_, _) => _shares.Value = _shares.Maximum, 60);
        var row = new FlowLayoutPanel { Dock = DockStyle.Top, AutoSize = true, Padding = new Padding(10, 4, 10, 4), WrapContents = false };
        var label = Ui.Label($"{survivor.Name} shares to take (up to {Currency.Number(most)}):");
        foreach (Control c in new Control[] { label, _shares, max }) c.Anchor = AnchorStyles.Left;
        row.Controls.AddRange([label, _shares, max]);
        _summary.Dock = DockStyle.Top;
        _summary.Padding = new Padding(12, 2, 12, 2);

        var keep = Ui.Button("Keep the cash", (_, _) => { _shares.Value = 0; DialogResult = DialogResult.OK; }, 120);
        var convert = Ui.Button("Convert", (_, _) => DialogResult = DialogResult.OK);
        AcceptButton = convert;
        CancelButton = keep;

        Controls.Add(_summary);
        Controls.Add(row);
        Controls.Add(intro);
        Controls.Add(Ui.ButtonBar(keep, convert));
        UpdateSummary(currency, price);
        ResumeLayout(false);
        PerformLayout();
    }

    /// <summary>The number of survivor shares chosen.</summary>
    public BigInteger Shares => new(_shares.Value);

    private void UpdateSummary(Currency currency, long price) =>
        _summary.Text = $"Converts {currency.Format(Shares * price)}; the rest stays as cash.";
}
