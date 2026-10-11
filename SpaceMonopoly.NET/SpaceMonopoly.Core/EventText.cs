namespace SpaceMonopoly.Core;

/// <summary>Plain-English descriptions of game events, used by the game log and the Claude prompt.</summary>
public static class EventText
{
    public static string Describe(Game game, GameEvent e, Currency currency)
    {
        string Name(int p) => game.Players[p].Name;
        string Co(int c) => Company.Names[c];

        switch (e)
        {
            case DiamondPlacedEvent d:
                return $"{Name(d.Player)} placed a star at {d.At} — no company formed.";

            case CompanyFoundedEvent f:
                return $"{Name(f.Player)} founded {Co(f.Company)} at {currency.Format(f.SharePrice)} a share, " +
                       $"getting {f.FreeShares} free shares and {currency.Format(f.Bonus)}.";

            case CompanyExpandedEvent x:
            {
                string text = $"{Co(x.Company)} grew: share price up {currency.Format(x.ValueAdded)} to {currency.Format(x.NewValue)}.";
                if (x.Dividends.Count > 0)
                    text += " Dividends: " + string.Join(", ", x.Dividends.Select(d => $"{Name(d.Player)} {currency.Format(d.Amount)}")) + ".";
                return text;
            }

            case MergerEvent m:
            {
                string text = $"Merger! {Co(m.Absorbed)} was taken over by {Co(m.Survivor)}; " +
                              $"{Co(m.Survivor)} shares are now {currency.Format(m.NewValue)}.";
                var paid = m.Rows.Where(r => r.Total > 0).ToList();
                if (paid.Count > 0)
                    text += " Payouts: " + string.Join(", ", paid.Select(r => $"{Name(r.Player)} {currency.Format(r.Total)}" +
                        (r.Bonus > 0 ? $" (including a {currency.Format(r.Bonus)} majority bonus)" : ""))) + ".";
                return text;
            }

            case SplitEvent s:
                return $"{Co(s.Company)} shares split two for one: {currency.Format(s.OldValue)} → {currency.Format(s.NewValue)}.";

            case EventCardEvent c:
            {
                string text = $"Round {c.Round} event: {EventCards.Title(c.Card)}. ";
                text += c.Card switch
                {
                    EventCard.PirateRaid or EventCard.InsiderScandal when c.Company >= 0 =>
                        $"{Co(c.Company)} shares fall from {currency.Format(c.OldPrice)} to {currency.Format(c.NewPrice)}.",
                    EventCard.AsteroidStrike when c.Company >= 0 =>
                        $"{Co(c.Company)} loses its square at {c.Square} and its shares fall from {currency.Format(c.OldPrice)} to {currency.Format(c.NewPrice)}.",
                    EventCard.PlanetDiscovered when c.Square is Pos at => $"A new planet appears at {at}.",
                    EventCard.GalacticGrant when c.Payments is [var p] => $"{Name(p.Player)} receives {currency.Format(p.Amount)}.",
                    EventCard.GalacticGrant => "No one is far enough behind, so nothing happens.",
                    EventCard.TaxAudit when c.Payments is { Count: > 0 } =>
                        "Taxed: " + string.Join(", ", c.Payments.Select(p => $"{Name(p.Player)} {currency.Format(-p.Amount)}")) + ".",
                    EventCard.QuietQuarter or EventCard.SolarFlare or EventCard.MergerMania or EventCard.MarketBoom => EventCards.Description(c.Card),
                    _ => "It has no effect this time.",
                };
                return text;
            }

            case FinalPayoutEvent f:
                return $"Final payout from {Co(f.Company)} (shares at {currency.Format(f.Price)}): " +
                       string.Join(", ", f.Payouts.Select(p => $"{Name(p.Player)} {currency.Format(p.Amount)}")) + ".";

            case SecretGoalEvent g:
                return $"{Name(g.Player)}'s secret goal: {SecretGoals.Description(g.Goal)} " +
                       (g.Reward > 0 ? $"Achieved: {currency.Format(g.Reward)}." : "Not achieved.");

            case NoMoreCompaniesEvent n:
                return $"No more companies can be formed on {(game.Type == GameType.Tournament ? Name(n.Player) + "'s board" : "the board")}. " +
                       $"The game will end after round {n.LastRound}.";

            case TradeEvent t:
                return $"{Name(t.Player)} {(t.IsBuy ? "bought" : "sold")} {Currency.Number(t.Quantity)} {Co(t.Company)} " +
                       $"share{(t.Quantity == 1 ? "" : "s")} for {currency.Format(t.Total)}.";

            default:
                return e.ToString();
        }
    }
}
