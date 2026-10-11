namespace SpaceMonopoly.Core.Ai;

/// <summary>What the Hard strategy would do this turn, with a one-line reason (F9, and the S14 Advisor).</summary>
/// <param name="Choice">The square's number in the current player's offer or hand (1-based).</param>
public sealed record Advice(Pos Square, int Choice, MovePreview Preview, IReadOnlyList<TradeOrder> Trades, string Reason);

public static class Advisor
{
    /// <summary>
    /// Works out on a copy of the game which square Hard would play and what it would trade afterwards.
    /// The real game is untouched, and Hard isn't allowed to swap or prospect here, so the advice uses the current hand.
    /// </summary>
    public static Advice Suggest(Game game, Currency? currency = null)
    {
        currency ??= Currency.Default;
        var hard = new RuleBasedAi(AiLevel.Hard, seed: game.Turn * 31 + game.CurrentPlayerIndex);
        var sim = game.Clone();
        var square = hard.ChooseSquare(sim, mayChangeHand: false);
        var preview = MovePreview.For(game, square);

        sim.PlaceSquare(square);
        var trades = hard.PlanTrades(sim);
        return new Advice(square, game.Choices.ToList().IndexOf(square) + 1, preview, trades, Reason(game, preview, trades, currency));
    }

    private static string Reason(Game game, MovePreview preview, IReadOnlyList<TradeOrder> trades, Currency currency)
    {
        int me = game.CurrentPlayerIndex;
        var mine = preview.NetWorthChange[me];
        int leader = Enumerable.Range(0, game.Players.Count).Where(i => i != me).OrderByDescending(game.NetWorth).FirstOrDefault(-1);

        string gain = mine > 0 ? $"it gains you {currency.Format(mine)}" : mine < 0 ? $"it costs you {currency.Format(-mine)}" : "it gains you nothing now";
        if (leader >= 0 && !game.Rules.HideDetails)
        {
            var theirs = preview.NetWorthChange[leader];
            gain += theirs == 0 ? $" and the leading opponent ({game.Players[leader].Name}) nothing"
                : $" against {currency.Format(theirs)} for the leading opponent ({game.Players[leader].Name})";
        }

        string buys = trades.Count == 0 ? "" : "; then " + string.Join(", ", trades.Select(t =>
            $"{(t.Action == TradeAction.Buy ? "buy" : "sell")} {(t.Quantity == 0 ? (t.Action == TradeAction.Buy ? "as many as possible of" : "all") : t.Quantity.ToString())} {Company.Names[t.Company]}"));
        return $"{preview.Summary(currency)}: {gain}{buys}.";
    }
}
