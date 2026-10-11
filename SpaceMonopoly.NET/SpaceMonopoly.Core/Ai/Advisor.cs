namespace SpaceMonopoly.Core.Ai;

/// <summary>What the Hard strategy would do this turn, with a one-line reason (F9, and the S14 Advisor).</summary>
/// <param name="Choice">The square's number in the current player's offer or hand (1-based), or 0 when advising on trades only.</param>
public sealed record Advice(Pos? Square, int Choice, MovePreview? Preview, IReadOnlyList<TradeOrder> Trades, string Reason);

public static class Advisor
{
    /// <summary>The seed the Advisor's Hard player uses, so the same position always gets the same advice.</summary>
    public static int SeedFor(Game game) => game.Turn * 31 + game.CurrentPlayerIndex;

    /// <summary>
    /// What Hard would do now: while choosing a square, which square and the trades after it; while trading, the trades.
    /// It works on a copy of the game, so the real game is untouched, and it isn't allowed to swap or prospect.
    /// It sees only what the player can: their own hand and the event card already announced.
    /// </summary>
    public static Advice Suggest(Game game, Currency? currency = null)
    {
        currency ??= Currency.Default;
        var hard = new RuleBasedAi(AiLevel.Hard, SeedFor(game));
        var sim = game.Clone();

        if (game.Phase != GamePhase.ChoosingSquare)
        {
            var orders = hard.PlanTrades(sim);
            return new Advice(null, 0, null, orders, TradeReason(game, orders, currency) is { Length: > 0 } why ? why : "Keep what you have this turn.");
        }

        var square = hard.ChooseSquare(sim, mayChangeHand: false);
        var preview = MovePreview.For(game, square);
        sim.PlaceSquare(square);
        var trades = hard.PlanTrades(sim);
        string reason = $"{preview.Summary(currency)}: {SquareReason(game, preview, currency)}";
        if (TradeReason(sim, trades, currency) is { Length: > 0 } then) reason += $" Then {then}";
        return new Advice(square, game.Choices.ToList().IndexOf(square) + 1, preview, trades, reason);
    }

    /// <summary>Names the main source of gain for the player: growth, a merger bonus, the survivor's jump, or none.</summary>
    private static string SquareReason(Game game, MovePreview preview, Currency currency)
    {
        int me = game.CurrentPlayerIndex;
        var mine = preview.NetWorthChange[me];
        string source = preview.Kind switch
        {
            MoveKind.Merge when preview.Events.OfType<MergerEvent>().SelectMany(m => m.Rows).Any(r => r.Player == me && r.Bonus > 0) =>
                "you collect a merger bonus",
            MoveKind.Merge when game.CurrentPlayer.Shares[preview.Company] > 0 =>
                $"you hold {Company.Names[preview.Company]}, which jumps in price as the survivor",
            MoveKind.Merge => "it merges companies",
            MoveKind.Grow when game.CurrentPlayer.Shares[preview.Company] > 0 =>
                $"you hold {Currency.Number(game.CurrentPlayer.Shares[preview.Company])} {Company.Names[preview.Company]} shares, so its growth is yours",
            MoveKind.Grow => "it grows a company you could buy into",
            MoveKind.Found => "you found a company and get its free shares",
            _ => "nothing better is available",
        };
        string gain = mine > 0 ? $"{currency.Format(mine)} for you" : mine < 0 ? $"it costs you {currency.Format(-mine)}" : "no gain for you yet";

        int leader = Enumerable.Range(0, game.Players.Count).Where(i => i != me).OrderByDescending(game.NetWorth).FirstOrDefault(-1);
        if (leader >= 0 && !game.Rules.HideDetails && preview.NetWorthChange[leader] != 0)
            gain += $", {currency.Format(preview.NetWorthChange[leader])} for {game.Players[leader].Name}";
        return $"{source} ({gain}).";
    }

    /// <summary>The suggested trades in words, saying when a purchase makes the player a company's largest or second-largest holder.</summary>
    private static string TradeReason(Game game, IReadOnlyList<TradeOrder> trades, Currency currency)
    {
        if (trades.Count == 0) return "";
        int me = game.CurrentPlayerIndex;
        var parts = new List<string>();
        foreach (var t in trades)
        {
            var c = game.Companies[t.Company];
            string amount = t.Quantity == 0 ? (t.Action == TradeAction.Buy ? "as many as you can of" : "all your") : Currency.Number(t.Quantity);
            string text = $"{(t.Action == TradeAction.Buy ? "buy" : "sell")} {amount} {c.Name}";
            if (game.IsModern && t.Action == TradeAction.Buy && t.Quantity > 0)
            {
                var after = game.CurrentPlayer.Shares[t.Company] + t.Quantity;
                int ahead = Enumerable.Range(0, game.Players.Count).Count(p => p != me && game.Players[p].Shares[t.Company] >= after);
                if (after >= ModernRules.MinSharesForBonus && ahead < 2)
                    text += ahead == 0 ? " (to be its largest holder)" : " (to be its second-largest holder)";
            }
            parts.Add(text);
        }
        return string.Join(", ", parts) + ".";
    }
}
