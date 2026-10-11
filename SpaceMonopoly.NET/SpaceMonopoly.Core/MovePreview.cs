namespace SpaceMonopoly.Core;

public enum MoveKind { Star, Found, Grow, Merge }

/// <summary>
/// What playing an offered square would do, worked out by playing it on a copy of the game so it always
/// matches the real rules.
/// </summary>
/// <param name="Company">The company that is founded or grows, or the survivor of a merger; -1 for a star.</param>
/// <param name="Absorbed">The company taken over in a merger, or -1.</param>
/// <param name="PriceBefore">The company's share price before the move (0 when it is founded).</param>
/// <param name="PriceRise">How much the share price goes up, before any split.</param>
/// <param name="PriceAfter">The share price afterwards, after any split.</param>
/// <param name="FounderCash">Cash the founder receives when a company is founded.</param>
/// <param name="NetWorthChange">How much each player's net worth changes, indexed by player.</param>
/// <param name="Allowed">Null if the square may be played, otherwise why not (forced mergers or expansion).</param>
public sealed record MovePreview(
    Pos At,
    MoveKind Kind,
    int Company,
    int Absorbed,
    long PriceBefore,
    long PriceRise,
    long PriceAfter,
    long FounderCash,
    bool Splits,
    IReadOnlyList<BigInteger> NetWorthChange,
    IReadOnlyList<GameEvent> Events,
    string? Allowed)
{
    /// <summary>A short mark for the board: a company letter for growth, M for a merger, + to found, ★ for a star.</summary>
    public string Label => Kind switch
    {
        MoveKind.Grow => Core.Company.LetterFor(Company).ToString(),
        MoveKind.Merge => "M",
        MoveKind.Found => "+",
        _ => "★",
    };

    /// <summary>Works out what each square on offer to the current player would do.</summary>
    public static IReadOnlyList<MovePreview> ForChoices(Game game) => game.Choices.Select(p => For(game, p)).ToList();

    /// <summary>Works out what playing this square would do for the current player.</summary>
    public static MovePreview For(Game game, Pos p)
    {
        var sim = game.Clone();
        sim.OfferOnlyForPreview(p);
        var events = sim.PlaceSquare(p);

        var change = Enumerable.Range(0, game.Players.Count).Select(i => sim.NetWorth(i) - game.NetWorth(i)).ToList();
        bool splits = events.OfType<SplitEvent>().Any();
        string? allowed = game.ValidateChoice(p);

        if (events.OfType<MergerEvent>().FirstOrDefault() is { } merger)
        {
            // With three or more companies meeting, the last merger names the final survivor.
            int survivor = events.OfType<MergerEvent>().Last().Survivor;
            long before = game.Companies[survivor].Value;
            long rise = events.OfType<CompanyExpandedEvent>().LastOrDefault()?.NewValue - before ?? 0;
            return new(p, MoveKind.Merge, survivor, merger.Absorbed, before, rise, sim.Companies[survivor].Value, 0, splits, change, events, allowed);
        }
        if (events.OfType<CompanyFoundedEvent>().FirstOrDefault() is { } founded)
            return new(p, MoveKind.Found, founded.Company, -1, 0, founded.SharePrice, sim.Companies[founded.Company].Value, founded.Bonus,
                splits, change, events, allowed);
        if (events.OfType<CompanyExpandedEvent>().FirstOrDefault() is { } grown)
            return new(p, MoveKind.Grow, grown.Company, -1, game.Companies[grown.Company].Value, grown.ValueAdded,
                sim.Companies[grown.Company].Value, 0, splits, change, events, allowed);
        return new(p, MoveKind.Star, -1, -1, 0, 0, 0, 0, false, change, events, allowed);
    }

    /// <summary>One line for the side panel, e.g. "Grow Atlantis Ltd. +₲600 a share (you +₲9,600)".</summary>
    public string Summary(Currency currency, int? you = null)
    {
        string name = Company >= 0 ? Core.Company.Names[Company] : "";
        string text = Kind switch
        {
            MoveKind.Grow => $"Grow {name} +{currency.Format(PriceRise)} a share",
            MoveKind.Merge => $"Merger: {name} takes over {Core.Company.Names[Absorbed]} (+{currency.Format(PriceRise)} a share)",
            MoveKind.Found => $"Found {name} at {currency.Format(PriceRise)}",
            _ => "Star (nothing grows)",
        };
        if (Splits) text += ", then splits";
        if (you is int me && NetWorthChange[me] != 0)
            text += $" (you {(NetWorthChange[me] > 0 ? "+" : "")}{currency.Format(NetWorthChange[me])})";
        return text;
    }

    /// <summary>A fuller description for a tooltip: the price change and who gains.</summary>
    public string Details(Game game, Currency currency)
    {
        var lines = new List<string>();
        string Co(int c) => Core.Company.Names[c];
        switch (Kind)
        {
            case MoveKind.Grow:
                lines.Add($"Grow {Co(Company)}: share price {currency.Format(PriceBefore)} → {currency.Format(PriceBefore + PriceRise)}" +
                          (Splits ? $", then splits to {currency.Format(PriceAfter)}." : "."));
                break;
            case MoveKind.Merge:
                lines.Add($"Merger: {Co(Company)} takes over {Co(Absorbed)}. {Co(Company)} shares become {currency.Format(PriceAfter)}" +
                          (Splits ? " after a split." : "."));
                var bonuses = Events.OfType<MergerEvent>().SelectMany(m => m.Rows).Where(r => r.Bonus > 0)
                    .Where(r => !game.Rules.HideDetails || r.Player == game.CurrentPlayerIndex)
                    .Select(r => $"{(r.Player == game.CurrentPlayerIndex ? "you" : game.Players[r.Player].Name)} {currency.Format(r.Bonus)}")
                    .ToList();
                var paid = Events.OfType<MergerEvent>().SelectMany(m => m.Rows).Where(r => r.Profit > 0)
                    .Where(r => !game.Rules.HideDetails || r.Player == game.CurrentPlayerIndex)
                    .Select(r => $"{(r.Player == game.CurrentPlayerIndex ? "you" : game.Players[r.Player].Name)} {currency.Format(r.Profit)}")
                    .ToList();
                if (paid.Count > 0) lines.Add($"Paid for {Co(Absorbed)} shares: " + string.Join(" · ", paid) + ".");
                if (bonuses.Count > 0) lines.Add("Majority bonus: " + string.Join(" · ", bonuses) + ".");
                else if (game.IsModern && !Events.OfType<MergerEvent>().SelectMany(m => m.Rows).Any(r => r.Bonus > 0))
                    lines.Add($"No majority bonus: nobody holds the {ModernRules.MinSharesForBonus} shares needed to qualify.");
                if (game.IsModern && bonuses.Count > 0) lines.Add($"(Holders of at least {ModernRules.MinSharesForBonus} shares qualify.)");
                if (game.IsModern && paid.Count > 0)
                    lines.Add($"Those paid may turn the money into {Co(Company)} shares at {currency.Format(PriceAfter)}.");
                break;
            case MoveKind.Found:
                long free = Events.OfType<CompanyFoundedEvent>().First().FreeShares;
                lines.Add($"Found {Co(Company)} at {currency.Format(PriceRise)} a share. You get {free} free shares" +
                          (FounderCash > 0 ? $" and {currency.Format(FounderCash)}." : ".") + (Splits ? $" It splits straight away to {currency.Format(PriceAfter)}." : ""));
                break;
            default:
                lines.Add("Places a star. No company grows or forms, but a star next to a company adds to its value later.");
                break;
        }

        // With "Hide player details" on, only your own change is shown.
        var gains = NetWorthChange.Select((amount, i) => (amount, i))
            .Where(t => t.amount != 0 && (!game.Rules.HideDetails || t.i == game.CurrentPlayerIndex))
            .Select(t => $"{(t.i == game.CurrentPlayerIndex ? "You" : game.Players[t.i].Name)} {(t.amount > 0 ? "+" : "")}{currency.Format(t.amount)}")
            .ToList();
        if (gains.Count > 0) lines.Add("Net worth: " + string.Join(" · ", gains));
        else if (Kind is MoveKind.Grow or MoveKind.Merge)
            lines.Add(game.Rules.HideDetails ? "You hold no shares in it, so you gain nothing now." : "Nobody holds shares in it yet, so nobody gains now.");
        if (Allowed is not null) lines.Add("Not allowed: " + Allowed);
        return string.Join(Environment.NewLine, lines);
    }
}
