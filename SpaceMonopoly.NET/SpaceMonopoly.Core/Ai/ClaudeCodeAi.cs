using System.Text;
using System.Text.Json;

namespace SpaceMonopoly.Core.Ai;

/// <summary>
/// A computer player that asks Claude (through Claude Code) for each move. One request per turn covers
/// both the square and the trades. If Claude can't be reached or gives an unusable answer, the turn is
/// played by the Hard built-in strategy instead and the reason is reported in <see cref="LastComment"/>.
/// </summary>
public sealed class ClaudeCodeAi : IAiPlayer
{
    /// <summary>
    /// The instructions for this game: only its own rules (Classic, Tournament or Modern, taken from the rule
    /// constants so they can't drift from the code), a strategy guide for that mode, and how to answer (S13).
    /// </summary>
    internal static string BuildSystemPrompt(Game game, Personality personality = Personality.None)
    {
        var sb = new StringBuilder();
        sb.AppendLine("You are playing Space Monopoly, a board game about founding companies and trading their shares, against other players.");
        sb.AppendLine("Your goal is to finish with the highest net worth: cash plus shares at their final price" +
                      (game.IsModern ? ", plus end-of-game payouts and your secret goal." : "."));
        sb.AppendLine();
        sb.AppendLine("RULES");
        sb.AppendLine(game.IsModern
            ? $"- You hold a private hand of {ModernRules.HandSize} squares (5 while you are alone in last place). Each turn you play one, then draw a new one."
            : "- Each turn you must pick one of the numbered squares offered on the map.");
        sb.AppendLine("- A square next to a planet (o) or star (*) but not next to a company founds a new company, if the company limit hasn't been reached.");
        sb.AppendLine(game.IsModern
            ? $"  The founder gets {ModernRules.FreeShares} free shares and no cash."
            : $"  The founder gets {Game.FreeShares} free shares plus cash equal to the starting share price.");
        sb.AppendLine($"- A square next to exactly one company grows it. The share price rises by {Game.SquareValue}, plus {Game.CircleBonus} for each planet next to the " +
                      $"new square, plus {Game.DiamondBonus} for each star next to it (stars are absorbed into the company).");
        sb.AppendLine(game.IsModern
            ? "  There is no dividend: holders gain only through the higher share price."
            : "  Every shareholder is also paid a cash dividend of (shares held x price rise).");
        sb.AppendLine("- A square next to two or more companies merges them. The bigger company survives (ties go to the earlier letter).");
        if (game.IsModern)
        {
            sb.AppendLine($"  The survivor's price rises by half the absorbed company's price. Shares in the absorbed company are bought back in cash at its price " +
                          $"(you get no survivor shares). Its largest holder gets a bonus of {ModernRules.LargestHolderBonus}x its share price and the second-largest " +
                          $"{ModernRules.SecondHolderBonus}x; you need at least {ModernRules.MinSharesForBonus} shares to qualify. Players tied for largest share both bonuses; " +
                          "players tied for second share the second bonus. Each player paid at a merger may turn any part of that money into survivor shares " +
                          "at the survivor's new price; this doesn't count toward the buy limit.");
        }
        else
        {
            sb.AppendLine("  Holders of the absorbed company are paid its share price in cash for each share AND get the same number of shares in the survivor. " +
                          "The survivor's price rises by the absorbed company's whole price.");
        }
        sb.AppendLine("- Any other square just places a star.");
        if (game.Board.AllPositions().Any(p => Board.IsSpecial(game.Board[p])))
            sb.AppendLine("- Special squares (map: n nebula, x black hole, w wormhole) can't be played. A square next to a nebula gets double the planet bonus; " +
                          "squares next to a black hole can't be played; the two wormhole ends count as next to each other, so companies can grow and merge through them.");
        if (game.IsModern)
        {
            sb.AppendLine($"- A new company has {ModernRules.SharesPerCompany:N0} shares shared by all players. It splits (price halves, every holding and its share count double) " +
                          $"the first time it has {ModernRules.FirstSplitSquares}+ squares and a price of {ModernRules.FirstSplitPrice:N0}+, and again at " +
                          $"{ModernRules.SecondSplitSquares}+ squares and {ModernRules.SecondSplitPrice:N0}+; never more than twice.");
            sb.AppendLine($"- After playing a square you may buy up to {ModernRules.BuyLimitPerTurn} shares this turn (across all companies) and sell any number.");
            sb.AppendLine($"- Cash earns {ModernRules.InterestPercent}% interest at the start of each of your turns.");
            sb.AppendLine($"- At the end of the game every company of {ModernRules.FinalPayoutMinSquares} or more squares pays its two largest holders " +
                          $"{(double)ModernRules.LargestHolderBonus / ModernRules.FinalPayoutDivisor}x and {(double)ModernRules.SecondHolderBonus / ModernRules.FinalPayoutDivisor}x " +
                          $"its share price (same {ModernRules.MinSharesForBonus}-share minimum; a sole holder gets only the first). " +
                          $"Shares bought in the last {ModernRules.LateBuyRounds} rounds don't count for this ranking.");
            sb.AppendLine("- From round 3 an event card is played at the start of each round; the next round's card is always announced in advance.");
            sb.AppendLine("- Each player has a secret goal worth a cash reward at the end.");
            sb.AppendLine("- Once per turn, before playing, you may swap one square in your hand for a new one. Once per game you may prospect: replace your whole hand.");
            sb.AppendLine($"- A square you hold for {ModernRules.SquareExpiryTurns} of your turns without playing it expires at the end of the last one and is replaced.");
        }
        else
        {
            sb.AppendLine($"- Share prices of {Game.SplitThreshold:N0} or more split two-for-one: the price halves and everyone's shares double.");
            sb.AppendLine("- Share prices never fall, and cash earns nothing.");
            sb.AppendLine("- After placing your square you may buy and sell any number of shares at the current prices.");
        }
        if (game.Type == GameType.Tournament)
            sb.AppendLine("- Tournament: everyone started from the same board, but the board and companies shown are yours alone. Opponents play on their own copies.");
        sb.AppendLine();

        sb.AppendLine("STRATEGY");
        sb.AppendLine("- Judge every move by YOUR gain compared with the leader's gain, not by whether a rival also gains. A move that gives you " +
                      "+5,000 and an opponent +4,000 is a good move.");
        sb.AppendLine("- Buying into a company an opponent leads is often your best move: you gain exactly as much per share as they do. " +
                      "Put your money where the growth is.");
        sb.AppendLine("- Hold cash only for a reason: a move you're saving for. Idle money falls behind.");
        sb.AppendLine("- You're shown what the built-in Hard strategy would play. It is a strong player: follow it unless you have a specific " +
                      "reason not to, and if you don't follow it, say why in your comment.");
        if (game.IsModern)
        {
            sb.AppendLine("- Holding the company that SURVIVES a merger is a big money-maker: its price jumps by half the absorbed company's price.");
            sb.AppendLine($"- Race for first or second place (at least {ModernRules.MinSharesForBonus} shares) in companies likely to be ABSORBED, and in every company " +
                          "you expect to survive to the end: both pay majority bonuses. Defend a first place you already hold.");
            sb.AppendLine("- Use your hand to plan: keep squares you'll want later, and don't play a merger square while an opponent would win the bonus.");
            sb.AppendLine($"- Cash earns {ModernRules.InterestPercent}% a turn, which is less than a growing company usually gains: keep only what you have a use for.");
            sb.AppendLine("- Prepare for next round's announced event, and play towards your secret goal.");
            sb.AppendLine("- Swap a square that does nothing for you. Prospect only when your whole hand is poor.");
        }
        else
        {
            sb.AppendLine("- Stay fully invested in whatever is about to grow: every growth pays a dividend on top of the higher price, and cash earns nothing.");
            sb.AppendLine("- Buy the cheapest company that can grow: every square adds the same amount to the price whatever the price is, so cheap shares gain most per Galactic Buck.");
            sb.AppendLine("- Late in the game, sell companies that can no longer grow and move the money to ones that can.");
        }
        sb.AppendLine();

        if (personality != Personality.None)
        {
            sb.AppendLine("PERSONALITY");
            sb.AppendLine(Personalities.ClaudeInstructions(personality) + " Stay true to this style, but still play to win.");
            sb.AppendLine();
        }

        sb.AppendLine("ANSWER");
        sb.AppendLine("Answer with JSON only, using the schema you're given.");
        sb.AppendLine("- \"choice\": the number of the square you play.");
        sb.AppendLine("- \"trades\": done after your square is played, at the prices shown for that square's outcome. Quantity 0 means as many as possible (buy max or sell all).");
        sb.AppendLine("- \"comment\": one short sentence explaining your move, for the other players to read.");
        sb.AppendLine("- \"plan\": one or two sentences on what you intend next turn. It will be shown back to you then.");
        if (game.IsModern)
        {
            sb.AppendLine("- \"swap\" (optional): the number of a square to throw away and replace BEFORE playing. You'll then be asked again with the new hand.");
            sb.AppendLine("- \"prospect\" (optional): true to replace your whole hand before playing (once per game). You'll then be asked again.");
            sb.AppendLine("- \"convert\" (optional): if your square causes a merger and you're paid for shares in the company taken over, the percent " +
                          "(0-100) of that money to turn into shares of the surviving company at its new price. Leave it out to keep the cash.");
        }
        return sb.ToString();
    }

    internal const string Schema = """
        {"type":"object","properties":{"choice":{"type":"integer","minimum":1,"maximum":5},"trades":{"type":"array","items":{"type":"object","properties":{"action":{"type":"string","enum":["buy","sell"]},"company":{"type":"string","enum":["A","B","C","D","E","F","G","H","I","J","K","L"]},"quantity":{"type":"integer","minimum":0}},"required":["action","company","quantity"],"additionalProperties":false}},"comment":{"type":"string"},"plan":{"type":"string"},"swap":{"type":"integer","minimum":0,"maximum":5},"prospect":{"type":"boolean"},"convert":{"type":"integer","minimum":0,"maximum":100}},"required":["choice","trades","comment","plan"],"additionalProperties":false}
        """;

    private readonly IClaudeRunner _runner;
    private readonly RuleBasedAi _fallback;
    private IReadOnlyList<TradeOrder>? _pendingTrades;
    private bool _usedFallback;

    /// <summary>How this Claude player likes to play (S9); a paragraph is added to its instructions.</summary>
    public Personality Personality { get; init; }

    /// <summary>The plan Claude gave last turn, shown back to it this turn (only the latest is kept).</summary>
    internal string? LastPlan { get; private set; }

    public ClaudeCodeAi(ClaudeCodeSettings settings, RuleBasedAi fallback)
        : this(new ClaudeCodeRunner(settings), fallback)
    {
        KeyTurnsOnly = settings.KeyTurnsOnly;
    }

    /// <summary>Ask Claude only when a square would merge companies or found one; Hard plays routine turns (S9).</summary>
    public bool KeyTurnsOnly { get; init; }

    public ClaudeCodeAi(IClaudeRunner runner, RuleBasedAi fallback)
    {
        _runner = runner;
        _fallback = fallback;
    }

    public string? LastComment { get; private set; }

    public async Task<Pos> ChooseSquareAsync(Game game, CancellationToken cancellationToken)
    {
        _pendingTrades = null;
        _usedFallback = false;
        _convertPercent = null;

        // Routine turn: nothing on offer founds or merges a company, so Hard plays it without asking Claude.
        if (KeyTurnsOnly && !MovePreview.ForChoices(game).Any(m => m.Kind is MoveKind.Merge or MoveKind.Found))
        {
            _usedFallback = true;
            LastComment = null;
            return _fallback.ChooseSquare(game);
        }
        try
        {
            string system = BuildSystemPrompt(game, Personality);
            var answer = await _runner.RunAsync(system, BuildPrompt(game, LastPlan), Schema, cancellationToken);

            // A swap or prospect changes the hand, so Claude is asked again with the new one (at most twice).
            for (int ask = 0; ask < 2 && ApplyHandChange(game, answer) is string change; ask++)
            {
                string note = $"You {change}. Here is your new hand; choose your square and trades.";
                answer = await _runner.RunAsync(system, BuildPrompt(game, LastPlan, note), Schema, cancellationToken);
            }

            var (square, trades, comment) = ParseAnswer(game, answer);
            _pendingTrades = trades;
            _convertPercent = answer.TryGetProperty("convert", out var convert) && convert.ValueKind == JsonValueKind.Number ? convert.GetInt32() : null;
            LastComment = comment;
            if (answer.TryGetProperty("plan", out var plan) && plan.GetString() is { Length: > 0 } text) LastPlan = text.Trim();
            return square;
        }
        catch (Exception ex) when (ex is ClaudeCodeException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _usedFallback = true;
            LastComment = $"(Claude unavailable — {ex.Message} Playing this turn with the built-in Hard strategy.)";
            return _fallback.ChooseSquare(game);
        }
    }

    // Percent of its merger money Claude asked to convert on its own move (S15), or null to use the built-in judgement.
    private int? _convertPercent;

    /// <summary>
    /// On its own move Claude's answer can say what share to convert; otherwise (for example when another player's
    /// square merged a company it holds) the built-in Hard judgement decides, to avoid a separate request.
    /// </summary>
    public BigInteger ChooseConversion(Game game, ConversionOffer offer)
    {
        if (offer.Player == game.CurrentPlayerIndex && _convertPercent is int percent)
            return offer.MostShares(game.Companies[offer.Survivor].Value) * Math.Clamp(percent, 0, 100) / 100;
        return _fallback.ChooseConversion(game, offer);
    }

    public Task<IReadOnlyList<TradeOrder>> PlanTradesAsync(Game game, CancellationToken cancellationToken)
    {
        if (_usedFallback || _pendingTrades is null)
            return Task.FromResult(_fallback.PlanTrades(game));
        var trades = _pendingTrades;
        _pendingTrades = null;
        return Task.FromResult(trades);
    }

    /// <summary>Applies a swap or prospect from Claude's answer. Returns what happened, or null if the hand is unchanged.</summary>
    internal static string? ApplyHandChange(Game game, JsonElement answer)
    {
        if (answer.TryGetProperty("prospect", out var p) && p.ValueKind == JsonValueKind.True && game.CanProspect)
        {
            game.Prospect();
            return "prospected (replaced your whole hand)";
        }
        if (answer.TryGetProperty("swap", out var w) && w.ValueKind == JsonValueKind.Number && w.GetInt32() is int n
            && n >= 1 && n <= game.Choices.Count && game.CanSwap)
        {
            game.Swap(game.Choices[n - 1]);
            return $"swapped square {n} for a new one";
        }
        return null;
    }

    internal static (Pos Square, IReadOnlyList<TradeOrder> Trades, string Comment) ParseAnswer(Game game, JsonElement answer)
    {
        int choice = answer.GetProperty("choice").GetInt32();
        if (choice < 1 || choice > game.Choices.Count)
            throw new InvalidOperationException($"Claude picked square {choice}, which wasn't offered.");
        var square = game.Choices[choice - 1];
        if (game.ValidateChoice(square) is string why)
            throw new InvalidOperationException($"Claude picked square {choice}, which isn't allowed: {why}");

        var trades = new List<TradeOrder>();
        if (answer.TryGetProperty("trades", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in list.EnumerateArray())
            {
                string letter = t.GetProperty("company").GetString() ?? "";
                if (letter.Length != 1 || letter[0] < 'A' || letter[0] - 'A' >= game.Companies.Length) continue;
                var action = t.GetProperty("action").GetString() == "sell" ? TradeAction.Sell : TradeAction.Buy;
                var qty = BigInteger.TryParse(t.GetProperty("quantity").GetRawText(), out var q) && q > 0 ? q : BigInteger.Zero;
                trades.Add(new TradeOrder(action, letter[0] - 'A', qty));
            }
        }

        string comment = answer.TryGetProperty("comment", out var c) ? c.GetString() ?? "" : "";
        return (square, trades, comment.Trim());
    }

    /// <summary>
    /// Describes the position with the same information a human gets: each square's effect and every player's
    /// net-worth change, holdings and majorities, the events and the buying allowance (S13).
    /// </summary>
    internal static string BuildPrompt(Game game, string? lastPlan = null, string? note = null)
    {
        var currency = Currency.Default; // Claude sees the default unit whatever the player has chosen.
        int me = game.CurrentPlayerIndex;
        bool hidden = game.Rules.HideDetails || !game.SharedBoard;
        bool Visible(int p) => p == me || !hidden;
        string Who(int p) => p == me ? "you" : game.Players[p].Name;
        var sb = new StringBuilder();

        sb.AppendLine($"You are {game.Players[me].Name}. Round {game.Turn + 1} of {game.Rounds} ({game.Rounds - game.Turn - 1} after this one). " +
                      $"{game.Type} game, {game.Spec.Width}x{game.Spec.Height} board, {game.Players.Count} players, up to {game.Companies.Length} companies. " +
                      "Money is in Galactic Bucks (₲).");
        if (game.NoMoreCompanies)
            sb.AppendLine($"No more companies can be formed, so the game ends after round {game.Rounds}.");
        if (lastPlan is not null) sb.AppendLine($"Your plan last turn: {lastPlan}");
        if (note is not null) sb.AppendLine(note);
        if (game.IsModern)
        {
            if (game.GoalOf(me) is SecretGoal goal) sb.AppendLine($"Your secret goal: {SecretGoals.Description(goal)}");
            if (game.CurrentEvent is EventCard now) sb.AppendLine($"Event this round: {EventCards.Title(now)} ({game.DescribeEvent(now)})");
            if (game.NextEvent is EventCard next) sb.AppendLine($"Event in round {game.NextEventRound}: {EventCards.Title(next)} ({game.DescribeEvent(next)})");
            sb.AppendLine($"You can buy {game.BuyAllowanceLeft} more shares this turn. Swap available: {(game.CanSwap ? "yes" : "no")}. " +
                          $"Prospect available: {(game.CanProspect ? "yes" : "no")}.");
        }
        sb.AppendLine();

        sb.AppendLine($"Map (x across, y down). Key: . empty, o planet, * star, {Company.LetterFor(0)}-{Company.LetterFor(game.Companies.Length - 1)} company squares, " +
                      $"1-{game.Choices.Count} the squares you can play.");
        AppendMap(sb, game);
        sb.AppendLine();

        sb.AppendLine("Companies:");
        bool any = false;
        foreach (var c in game.Companies.Where(c => c.Exists))
        {
            any = true;
            sb.Append($"  {c.Letter} = {c.Name}: price {currency.Format(c.Value)}, {game.Board.CompanySize(c.Index)} squares");
            if (game.IsModern) sb.Append($", {game.SharesLeft(c.Index):N0} shares left");
            sb.Append(". Holdings:");
            for (int p = 0; p < game.Players.Count; p++)
                if (Visible(p)) sb.Append($" {Who(p)} {game.Players[p].Shares[c.Index]:N0};");
            if (game.IsModern && !hidden)
            {
                var bonuses = Game.MajorityBonuses(Enumerable.Range(0, game.Players.Count).Select(p => (p, game.Players[p].Shares[c.Index])), c.Value);
                sb.Append(bonuses.Count == 0
                    ? $" No one holds the {ModernRules.MinSharesForBonus} shares needed for a majority bonus."
                    : " Bonus now goes to: " + string.Join(", ", bonuses.OrderByDescending(b => b.Value).Select(b => $"{Who(b.Key)} {currency.Format(b.Value)}")) + ".");
            }
            sb.AppendLine();
        }
        if (!any) sb.AppendLine("  None yet.");
        sb.AppendLine();

        sb.AppendLine("Players:");
        for (int p = 0; p < game.Players.Count; p++)
        {
            var pl = game.Players[p];
            sb.AppendLine($"  {pl.Name}{(p == me ? " (you)" : "")}: net worth {currency.Format(game.NetWorth(p))}" +
                          (Visible(p) ? $", cash {currency.Format(pl.Money)}" : ""));
        }
        sb.AppendLine();

        sb.AppendLine(game.IsModern ? "Your hand:" : "Your options:");
        for (int i = 0; i < game.Choices.Count; i++)
        {
            var square = game.Choices[i];
            sb.Append($"  {i + 1}. Square {square}" + (game.IsModern && game.TurnsLeft(me, square) is int left and < 100
                ? (left <= 1 ? " (last turn before it expires)" : $" ({left} turns before it expires)") : "") + ": ");
            if (game.ValidateChoice(square) is string why)
            {
                sb.AppendLine($"NOT ALLOWED ({why})");
                continue;
            }
            var preview = MovePreview.For(game, square);
            var sim = game.Clone();
            sim.OfferOnlyForPreview(square);
            var events = sim.PlaceSquare(square);
            sb.AppendLine(string.Join(" ", events.Select(e => EventText.Describe(sim, e, currency))));
            var gains = preview.NetWorthChange.Select((amount, p) => (amount, p)).Where(t => t.amount != 0 && Visible(t.p))
                .Select(t => $"{Who(t.p)} {(t.amount > 0 ? "+" : "")}{currency.Format(t.amount)}").ToList();
            sb.AppendLine($"     Net worth change: {(gains.Count == 0 ? "none" : string.Join(", ", gains))}. " +
                          $"Prices afterwards: {string.Join(", ", sim.Companies.Where(c => c.Exists).Select(c => $"{c.Letter} {currency.Format(c.Value)}"))}.");
        }
        sb.AppendLine();
        // F9: start Claude from Hard's level; its own judgement can add to it.
        var advice = Advisor.Suggest(game, currency);
        sb.AppendLine($"The built-in Hard strategy would play square {advice.Choice}: {advice.Reason}");
        sb.AppendLine("Follow it unless you have a specific reason not to, and if you don't, name that reason in your comment.");
        sb.AppendLine();
        sb.AppendLine("Pick your square, any trades, and your plan for next turn.");
        return sb.ToString();
    }

    /// <summary>The position as Claude sees it, for asking Claude about it outside a turn (the Advisor's Explain).</summary>
    public static string DescribePosition(Game game) => BuildPrompt(game);

    private static void AppendMap(StringBuilder sb, Game game)
    {
        var board = game.Board;
        var choiceIndex = game.Choices.Select((p, i) => (p, i)).ToDictionary(t => t.p, t => t.i + 1);

        sb.Append("     ");
        for (int x = 0; x < board.Width; x++) sb.Append((x % 10).ToString());
        sb.AppendLine();
        for (int y = 0; y < board.Height; y++)
        {
            sb.Append($"  {y,2} ");
            for (int x = 0; x < board.Width; x++)
            {
                var pos = new Pos(x, y);
                int cell = board[pos];
                char ch = choiceIndex.TryGetValue(pos, out int n) ? (char)('0' + n)
                    : cell == Board.Circle ? 'o'
                    : cell == Board.Diamond ? '*'
                    : cell == Board.Nebula ? 'n'
                    : cell == Board.BlackHole ? 'x'
                    : cell == Board.Wormhole ? 'w'
                    : cell >= Board.FirstCompany ? Company.LetterFor(Board.CompanyOf(cell))
                    : '.';
                sb.Append(ch);
            }
            sb.AppendLine();
        }
    }
}
