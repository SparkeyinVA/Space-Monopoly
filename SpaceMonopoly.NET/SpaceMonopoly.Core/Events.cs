namespace SpaceMonopoly.Core;

/// <summary>Modern only: the cards in the events deck (S5 in CLAUDE.md). One is flipped at the start of every round.</summary>
public enum EventCard
{
    QuietQuarter,
    PirateRaid,
    AsteroidStrike,
    MarketBoom,
    SolarFlare,
    PlanetDiscovered,
    MergerMania,
    InsiderScandal,
    GalacticGrant,
    TaxAudit,
}

/// <summary>
/// How much luck a Modern game has: the number of Quiet Quarter cards in the deck.
/// None (no events at all) isn't offered in New Game; it's for tests and balance comparisons.
/// </summary>
public enum Chaos { Calm, Standard, Wild, None }

public static class EventCards
{
    /// <summary>Pirate Raid: the most expensive company loses this share of its price.</summary>
    public const int PirateRaidPercent = 25;

    /// <summary>Asteroid Strike: the share price drops by this much.</summary>
    public const long AsteroidLoss = 600;

    /// <summary>Market Boom: every share price rises by this share.</summary>
    public const int MarketBoomPercent = 10;

    /// <summary>Insider Scandal: the leader's biggest holding loses this share of its price.</summary>
    public const int ScandalPercent = 20;

    /// <summary>Galactic Grant: this share of the average net worth, paid to the poorest player (F5)...</summary>
    public const int GrantPercent = 10;

    /// <summary>...but only if their net worth is below this share of the leader's.</summary>
    public const int GrantBelowLeaderPercent = 75;

    /// <summary>No events in the first rounds (F6): the first card is flipped in this round.</summary>
    public const int FirstEventRound = 3;

    /// <summary>Tax Audit: players above the average net worth pay this share of their cash.</summary>
    public const int TaxPercent = 10;

    /// <summary>No event can push a share price below this.</summary>
    public const long MinimumPrice = 100;

    public static string Title(EventCard card) => card switch
    {
        EventCard.QuietQuarter => "Quiet Quarter",
        EventCard.PirateRaid => "Pirate Raid",
        EventCard.AsteroidStrike => "Asteroid Strike",
        EventCard.MarketBoom => "Market Boom",
        EventCard.SolarFlare => "Solar Flare",
        EventCard.PlanetDiscovered => "Planet Discovered",
        EventCard.MergerMania => "Merger Mania",
        EventCard.InsiderScandal => "Insider Scandal",
        EventCard.GalacticGrant => "Galactic Grant",
        EventCard.TaxAudit => "Tax Audit",
        _ => card.ToString(),
    };

    public static string Description(EventCard card) => card switch
    {
        EventCard.QuietQuarter => "Nothing happens.",
        EventCard.PirateRaid => $"The most expensive company loses {PirateRaidPercent}% of its share price.",
        EventCard.AsteroidStrike => $"A random company loses its newest square and {AsteroidLoss:N0} from its share price.",
        EventCard.MarketBoom => $"Every share price rises {MarketBoomPercent}%.",
        EventCard.SolarFlare => "No company can grow this round: squares that would grow or merge a company become stars.",
        EventCard.PlanetDiscovered => "A new planet appears on an empty square away from the companies.",
        EventCard.MergerMania => "Merger bonuses are doubled this round.",
        EventCard.InsiderScandal => $"The company the leader holds most of loses {ScandalPercent}% of its share price.",
        EventCard.GalacticGrant => $"The poorest player receives {GrantPercent}% of the average net worth, if they're below {GrantBelowLeaderPercent}% of the leader.",
        EventCard.TaxAudit => $"Players above the average net worth pay {TaxPercent}% of their cash.",
        _ => "",
    };

    /// <summary>The deck for a chaos level: 14 event cards plus 12, 6 or 2 Quiet Quarters.</summary>
    public static List<EventCard> Deck(Chaos chaos)
    {
        if (chaos == Chaos.None) return [EventCard.QuietQuarter];
        var deck = new List<EventCard>();
        void Add(EventCard card, int copies) => deck.AddRange(Enumerable.Repeat(card, copies));
        Add(EventCard.QuietQuarter, chaos switch { Chaos.Calm => 12, Chaos.Wild => 2, _ => 6 });
        Add(EventCard.PirateRaid, 2);
        Add(EventCard.AsteroidStrike, 2);
        Add(EventCard.MarketBoom, 2);
        Add(EventCard.SolarFlare, 2);
        Add(EventCard.PlanetDiscovered, 2);
        Add(EventCard.MergerMania, 1);
        Add(EventCard.InsiderScandal, 1);
        Add(EventCard.GalacticGrant, 1);
        Add(EventCard.TaxAudit, 1);
        return deck;
    }
}

/// <summary>
/// The round's event card took effect. Depending on the card: <paramref name="Company"/> and the prices it changed,
/// a <paramref name="Square"/> that was emptied or became a planet, and the players who were paid or taxed.
/// </summary>
public sealed record EventCardEvent(
    EventCard Card,
    int Round,
    int Company = -1,
    long OldPrice = 0,
    long NewPrice = 0,
    Pos? Square = null,
    IReadOnlyList<Payout>? Payments = null) : GameEvent;
