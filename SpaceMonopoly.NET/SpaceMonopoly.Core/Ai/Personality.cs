namespace SpaceMonopoly.Core.Ai;

/// <summary>
/// How a computer or Claude player likes to play (S9). A personality changes how moves are weighted, not the
/// skill level: a Hard Hoarder is still Hard.
/// </summary>
public enum Personality { None, Raider, Tycoon, Hoarder, Gambler }

/// <summary>The weights a personality applies to the built-in players' choices.</summary>
/// <param name="MergerBias">Extra score for a square that causes a merger, as a share of the player's net worth.</param>
/// <param name="FoundBias">Extra score for a square that founds a company, as a share of the player's net worth.</param>
/// <param name="BonusWeight">How much a majority stake's bonus counts when buying (1 = its face value).</param>
/// <param name="CashKeep">Modern: share of net worth kept back as cash at the start, shrinking to nothing by the last round.</param>
/// <param name="GrowthCompanies">How many companies it spreads its growth buying over.</param>
/// <param name="SellAt">Hard sells holdings growing slower than this share of the best company's rate (lower: holds longer).</param>
/// <param name="EventCaution">How strongly it reacts to a bad event announced for next round (1 = Hard's normal reaction).</param>
public sealed record PersonalityProfile(
    double MergerBias,
    double FoundBias,
    double BonusWeight,
    double CashKeep,
    int GrowthCompanies,
    double SellAt,
    double EventCaution);

public static class Personalities
{
    public static readonly Personality[] Playable = [Personality.Raider, Personality.Tycoon, Personality.Hoarder, Personality.Gambler];

    public static PersonalityProfile Profile(Personality personality) => personality switch
    {
        Personality.Raider => new(MergerBias: 0.02, FoundBias: 0, BonusWeight: 1.3, CashKeep: 0, GrowthCompanies: int.MaxValue, SellAt: 0.6, EventCaution: 1),
        Personality.Tycoon => new(MergerBias: 0, FoundBias: 0.06, BonusWeight: 1.4, CashKeep: 0, GrowthCompanies: int.MaxValue, SellAt: 0.3, EventCaution: 1),
        Personality.Hoarder => new(MergerBias: 0, FoundBias: 0, BonusWeight: 1.4, CashKeep: 0.2, GrowthCompanies: int.MaxValue, SellAt: 0.6, EventCaution: 2),
        Personality.Gambler => new(MergerBias: 0.06, FoundBias: 0, BonusWeight: 1.0, CashKeep: 0, GrowthCompanies: 1, SellAt: 0.6, EventCaution: 0.5),
        _ => new(MergerBias: 0, FoundBias: 0, BonusWeight: 1.0, CashKeep: 0, GrowthCompanies: int.MaxValue, SellAt: 0.6, EventCaution: 1),
    };

    public static string Label(Personality personality) => personality == Personality.None ? "" : personality.ToString();

    public static string Description(Personality personality) => personality switch
    {
        Personality.Raider => "goes after mergers and majority bonuses",
        Personality.Tycoon => "founds companies and holds on to them",
        Personality.Hoarder => "keeps plenty of cash and avoids risk",
        Personality.Gambler => "bets heavily on one company and the riskiest mergers",
        _ => "",
    };

    /// <summary>The paragraph added to the Claude player's instructions (S13).</summary>
    public static string ClaudeInstructions(Personality personality) => personality switch
    {
        Personality.Raider => "You are a RAIDER. You love mergers: look for squares that merge companies and race to be the largest " +
                              "holder of companies about to be taken over, so you collect the bonuses. Your comments are bold and predatory.",
        Personality.Tycoon => "You are a TYCOON. You like founding companies and building them into the biggest on the board, " +
                              "and you hold your shares rather than trading in and out. Your comments are proud and grand.",
        Personality.Hoarder => "You are a HOARDER. You keep a good share of your money as cash, avoid companies an announced event " +
                               "will hurt, and buy in late when the picture is clear. Your comments are cautious and a little smug.",
        Personality.Gambler => "You are a GAMBLER. You put most of your money into one or two companies and take the riskiest merger " +
                               "plays when the payoff is big. Your comments are excitable and full of bravado.",
        _ => "",
    };

    /// <summary>Picks a personality at random for "Random" in New Game.</summary>
    public static Personality Random(Random rng) => Playable[rng.Next(Playable.Length)];
}
