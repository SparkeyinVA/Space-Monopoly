using System.Diagnostics;
using System.Text.Json;
using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.WinForms;

/// <summary>Currency, game rules, computer speed and Claude Code settings (Preferences.cpp in the original).</summary>
internal sealed class PreferencesForm : Form
{
    private readonly ComboBox _unit = new() { Width = 150 };
    private readonly RadioButton _prefix = new() { Text = "Before the amount (₲100)", AutoSize = true };
    private readonly RadioButton _postfix = new() { Text = "After the amount (100 ₲)", AutoSize = true };
    private readonly TrackBar _speed = new() { Minimum = 0, Maximum = 20, TickFrequency = 2, Width = 260 };
    private readonly Label _speedLabel = Ui.Label("");
    private readonly CheckBox _popups = new() { Text = "Show pop-up windows for new companies, mergers and splits (otherwise they appear in the news line)", AutoSize = true };
    private readonly CheckBox _tips = new() { Text = "Show a short tip the first time something happens (Modern)", AutoSize = true };
    private bool _resetTips;

    private readonly CheckBox _forceMergers = new() { Text = "Force mergers — if a merger is offered, it must be taken", AutoSize = true };
    private readonly CheckBox _forceExpansion = new() { Text = "Force expansion — if an expansion is offered, it must be taken", AutoSize = true };
    private readonly CheckBox _allowCreation = new() { Text = "Allow company creation — founding a company also counts as expanding", AutoSize = true };
    private readonly CheckBox _hideDetails = new() { Text = "Hide player details — players can't look at each other's holdings", AutoSize = true };

    private readonly TextBox _claudePath = new() { Width = 330 };
    private readonly ComboBox _claudeModel = new() { Width = 140 };
    private readonly ComboBox _claudeEffort = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly NumericUpDown _claudeTimeout = new() { Minimum = 30, Maximum = 900, Increment = 30, Width = 80 };
    private readonly CheckBox _claudeKeyTurns = new() { Text = "Ask Claude only on key turns (a merger or new company is possible); Hard plays the rest", AutoSize = true };
    private readonly Label _claudeStatus = Ui.Label("");
    private readonly Button _test;

    public PreferencesForm(AppSettings settings)
    {
        Settings = settings;
        SuspendLayout();
        Text = "Preferences";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Ui.KeepOnScreen(this);   // F17: never off the top of a second monitor
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96f, 96f);
        Font = new Font("Segoe UI", 9f);
        ClientSize = new Size(560, 400);

        _unit.Items.AddRange(["₲", "$", "£", "€", "¥", "₹", "Cr", "Credits", "Galactic Bucks"]);
        _unit.MaxLength = MaxUnitLength;
        _claudeModel.Items.AddRange(["sonnet", "opus", "haiku"]);
        _claudeEffort.Items.AddRange(["low", "medium", "high"]);

        var tabs = new TabControl { Dock = DockStyle.Fill };

        // General
        var general = Page("General");
        var unitRow = Row(Ui.Label("Currency (₲ = Galactic Bucks):"), _unit);
        var speedRow = Row(Ui.Label("Computer player speed:"), _speed, _speedLabel);
        var resetTips = Ui.Button("Reset tips", (s, _) => { _resetTips = true; ((Button)s!).Enabled = false; ((Button)s!).Text = "Tips reset"; }, 100);
        Ui.Tip(resetTips, "Show every first-time tip again.");
        general.Controls.AddRange([unitRow, _prefix, _postfix, Spacer(), speedRow, _popups, Row(_tips, resetTips)]);
        _speed.ValueChanged += (_, _) => _speedLabel.Text = _speed.Value == 0 ? "No pause" : $"{_speed.Value * 100} ms pause";

        // Game rules
        var rules = Page("Game");
        var rulesHelp = Ui.Label("These make multiplayer games fairer. The original author recommended at least " +
                                 "Force expansion and Allow company creation, plus Force mergers if you don't want to fall out with your mates.");
        rulesHelp.MaximumSize = new Size(500, 0);
        rules.Controls.AddRange([rulesHelp, Spacer(), _forceMergers, _forceExpansion, _allowCreation, _hideDetails]);
        _forceExpansion.CheckedChanged += (_, _) => _allowCreation.Enabled = _forceExpansion.Checked;

        // Claude
        var claude = Page("Claude");
        var claudeHelp = Ui.Label("Claude players run Claude Code in the background (\"claude -p\") and use your Claude subscription " +
                                  "login — no API key needed. Each move takes roughly 10-30 seconds and counts toward your plan's usage limits. " +
                                  "If Claude can't be reached, that turn is played by the built-in Hard strategy.");
        claudeHelp.MaximumSize = new Size(500, 0);
        var browse = Ui.Button("Browse...", (_, _) => Browse(), 80);
        _test = Ui.Button("Test Claude", async (_, _) => await TestAsync(), 110);
        claude.Controls.AddRange([
            claudeHelp, Spacer(),
            Row(Ui.Label("claude.exe location:"), _claudePath, browse),
            Row(Ui.Label("Model:"), _claudeModel, Ui.Label("Effort:"), _claudeEffort),
            Row(Ui.Label("Give up after (seconds):"), _claudeTimeout),
            _claudeKeyTurns,
            Row(_test, _claudeStatus),
        ]);
        _claudeStatus.MaximumSize = new Size(380, 0);

        tabs.TabPages.AddRange([TabFor(general, "General"), TabFor(rules, "Game rules"), TabFor(claude, "Claude")]);

        var ok = Ui.Button("OK", (_, _) => Save());
        var cancel = Ui.Button("Cancel");
        cancel.DialogResult = DialogResult.Cancel;
        AcceptButton = ok;
        CancelButton = cancel;

        Controls.Add(tabs);
        Controls.Add(Ui.ButtonBar(cancel, ok));

        LoadValues(settings);
        ResumeLayout(false);
        PerformLayout();
    }

    private const int MaxUnitLength = 16;

    public AppSettings Settings { get; private set; }

    private static FlowLayoutPanel Page(string name) => new()
    {
        Name = name,
        Dock = DockStyle.Fill,
        FlowDirection = FlowDirection.TopDown,
        WrapContents = false,
        AutoScroll = true,
        Padding = new Padding(10),
    };

    private static TabPage TabFor(Control content, string title)
    {
        var page = new TabPage(title);
        page.Controls.Add(content);
        return page;
    }

    private static FlowLayoutPanel Row(params Control[] controls)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Margin = new Padding(0, 2, 0, 2) };
        foreach (var c in controls) c.Anchor = AnchorStyles.Left;
        row.Controls.AddRange(controls);
        return row;
    }

    private static Control Spacer() => new Panel { Height = 8, Width = 10 };

    private void LoadValues(AppSettings s)
    {
        _unit.Text = s.CurrencyUnit;
        _prefix.Checked = s.CurrencyPrefix;
        _postfix.Checked = !s.CurrencyPrefix;
        _speed.Value = Math.Clamp(s.ComputerDelayMs / 100, _speed.Minimum, _speed.Maximum);
        _speedLabel.Text = _speed.Value == 0 ? "No pause" : $"{_speed.Value * 100} ms pause";
        _popups.Checked = s.ShowComputerPopups;
        _tips.Checked = s.ShowTips;

        _forceMergers.Checked = s.Rules.ForceMergers;
        _forceExpansion.Checked = s.Rules.ForceExpansion;
        _allowCreation.Checked = s.Rules.AllowCompanyCreation;
        _allowCreation.Enabled = s.Rules.ForceExpansion;
        _hideDetails.Checked = s.Rules.HideDetails;

        _claudePath.Text = s.Claude.ExecutablePath;
        _claudePath.PlaceholderText = ClaudeCodeRunner.ResolveExecutable("") ?? "Not found — install Claude Code";
        _claudeModel.Text = s.Claude.Model;
        _claudeEffort.SelectedItem = s.Claude.Effort;
        if (_claudeEffort.SelectedIndex < 0) _claudeEffort.SelectedIndex = 0;
        _claudeTimeout.Value = Math.Clamp(s.Claude.TimeoutSeconds, (int)_claudeTimeout.Minimum, (int)_claudeTimeout.Maximum);
        _claudeKeyTurns.Checked = s.Claude.KeyTurnsOnly;
    }

    private ClaudeCodeSettings ReadClaude() => new()
    {
        ExecutablePath = _claudePath.Text.Trim(),
        Model = string.IsNullOrWhiteSpace(_claudeModel.Text) ? "sonnet" : _claudeModel.Text.Trim(),
        Effort = (string?)_claudeEffort.SelectedItem ?? "low",
        TimeoutSeconds = (int)_claudeTimeout.Value,
        KeyTurnsOnly = _claudeKeyTurns.Checked,
    };

    private void Save()
    {
        string unit = _unit.Text.Trim();
        if (unit.Length is < 1 or > MaxUnitLength)
        {
            MessageBox.Show(this, $"The currency must be 1 to {MaxUnitLength} characters long.", "Preferences", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }
        Settings = Settings with
        {
            CurrencyUnit = unit,
            CurrencyPrefix = _prefix.Checked,
            ComputerDelayMs = _speed.Value * 100,
            ShowComputerPopups = _popups.Checked,
            ShowTips = _tips.Checked,
            SeenTips = _resetTips ? [] : Settings.SeenTips,
            Rules = new GameRules
            {
                ForceMergers = _forceMergers.Checked,
                ForceExpansion = _forceExpansion.Checked,
                AllowCompanyCreation = _forceExpansion.Checked && _allowCreation.Checked,
                HideDetails = _hideDetails.Checked,
            },
            Claude = ReadClaude(),
        };
        DialogResult = DialogResult.OK;
        Close();
    }

    private void Browse()
    {
        using var dialog = new OpenFileDialog { Filter = "Claude Code|claude.exe|Programs|*.exe", Title = "Find claude.exe" };
        if (dialog.ShowDialog(this) == DialogResult.OK) _claudePath.Text = dialog.FileName;
    }

    private async Task TestAsync()
    {
        _test.Enabled = false;
        _claudeStatus.ForeColor = SystemColors.ControlText;
        _claudeStatus.Text = "Asking Claude...";
        var watch = Stopwatch.StartNew();
        try
        {
            var runner = new ClaudeCodeRunner(ReadClaude());
            var answer = await runner.RunAsync(
                "You are checking that a game can reach you. Reply with a short, friendly greeting for the players.",
                "Say hello to the Space Monopoly players in one short sentence.",
                """{"type":"object","properties":{"greeting":{"type":"string"}},"required":["greeting"],"additionalProperties":false}""",
                CancellationToken.None);
            _claudeStatus.ForeColor = Color.DarkGreen;
            _claudeStatus.Text = $"Working ({watch.Elapsed.TotalSeconds:0} s): \"{answer.GetProperty("greeting").GetString()}\"";
        }
        catch (Exception ex) when (ex is ClaudeCodeException or JsonException or KeyNotFoundException)
        {
            _claudeStatus.ForeColor = Color.Firebrick;
            _claudeStatus.Text = ex.Message;
        }
        finally
        {
            _test.Enabled = true;
        }
    }
}
