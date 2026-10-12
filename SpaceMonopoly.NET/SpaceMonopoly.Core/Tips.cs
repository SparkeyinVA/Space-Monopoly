namespace SpaceMonopoly.Core;

/// <summary>
/// First-time tips (S12): a short tip the first time something happens to a human player in a Modern game.
/// The text is built from the rule settings, so it stays right when they're tuned.
/// </summary>
public static class Tips
{
    public const string Hand = "hand";
    public const string MergerSquare = "merger-square";
    public const string BuyLimit = "buy-limit";
    public const string CantAfford = "cant-afford";
    public const string EventComing = "event-coming";
    public const string SecretGoal = "secret-goal";
    public const string Split = "split";
    public const string ExtraSquare = "extra-square";
    public const string Discard = "discard";
    public const string LastRounds = "last-rounds";
    public const string SoldOut = "sold-out";
    public const string Specials = "specials";

    public static readonly IReadOnlyList<string> All =
        [Hand, MergerSquare, BuyLimit, CantAfford, EventComing, SecretGoal, Split, ExtraSquare, Discard, LastRounds, SoldOut, Specials];

    public static string Text(string id, Currency? currency = null)
    {
        var c = currency ?? Currency.Default;
        return id switch
        {
            Hand => $"Your hand: you hold {ModernRules.HandSize} squares that only you can see. Play one each turn and you draw a new one. " +
                    "You can swap one square a turn, and take a whole new hand once per game.",
            MergerSquare => $"You hold a merger square (marked M). The company taken over pays its two largest holders a bonus of " +
                            $"{ModernRules.LargestHolderBonus}× and {ModernRules.SecondHolderBonus}× its share price, doubled for each split it has had " +
                            $"(at least {ModernRules.MinSharesForBonus} shares to qualify). Try to lead that company before you play it.",
            BuyLimit => $"You've bought the most shares you can this turn ({ModernRules.BuyLimitPerTurn}). You can still sell; the limit resets next turn.",
            CantAfford => $"You can't afford a share right now. Cash earns {ModernRules.InterestPercent}% interest each turn, and selling shares raises cash.",
            EventComing => "Next round's event card is shown in the side panel. Plan for it: for example, avoid the target of a Pirate Raid.",
            SecretGoal => "You have a secret goal (shown in the side panel). Meet it by the end of the game for a cash reward; the others' goals are revealed then.",
            Split => $"A company has split: its share price halved and everyone's shares doubled, so your holding is worth the same. " +
                     $"Splits happen at {ModernRules.FirstSplitSquares} squares and {c.Format(ModernRules.FirstSplitPrice)}, then {ModernRules.SecondSplitSquares} " +
                     $"squares and {c.Format(ModernRules.SecondSplitPrice)}, and make bonuses bigger.",
            ExtraSquare => $"You're in last place, so you hold {ModernRules.HandSize + 1} squares this turn instead of {ModernRules.HandSize}, " +
                           "and the extra one helps a company you hold or founds a new one. Once you're no longer last, you'll choose one to throw away.",
            Discard => "Before you end your turn you may discard one square you don't want (Discard in Your hand). A different square arrives at " +
                       "the start of your next turn. Squares never run out of time, so keep the ones you're saving for later.",
            LastRounds => $"The last {ModernRules.LateBuyRounds} rounds: at the end each company of {ModernRules.FinalPayoutMinSquares}+ squares pays its two " +
                          "largest holders a final payout, but shares bought from now on don't count towards who holds the most.",
            SoldOut => "A company is sold out: every share is held. It can't be bought until a holder sells or it splits, which doubles its shares.",
            Specials => "This board has special squares: a nebula doubles the planet bonus next to it, nothing can be played beside a black hole, " +
                        "and the two wormhole ends count as next to each other.",
            _ => "",
        };
    }

    /// <summary>The tips that apply to the current human player right now (in order of priority).</summary>
    public static IEnumerable<string> Applicable(Game game)
    {
        if (!game.IsModern || game.Phase == GamePhase.Over || !game.CurrentPlayer.IsHuman) yield break;
        int me = game.CurrentPlayerIndex;
        if (game.Phase == GamePhase.ChoosingSquare)
        {
            yield return Hand;
            if (game.GoalOf(me) is not null) yield return SecretGoal;
            if (game.NextEvent is EventCard next && next != EventCard.QuietQuarter) yield return EventComing;
            if (game.Choices.Any(p => game.Board.Survey(p).Companies.Count > 1)) yield return MergerSquare;
            if (game.HandOf(me).Count > ModernRules.HandSize) yield return ExtraSquare;
            if (game.Board.AllPositions().Any(p => Board.IsSpecial(game.Board[p]))) yield return Specials;
        }
        else
        {
            if (game.BuyAllowanceLeft == 0) yield return BuyLimit;
            var live = game.Companies.Where(c => c.Exists).ToList();
            if (live.Count > 0 && live.All(c => c.Value > game.CurrentPlayer.Money)) yield return CantAfford;
            if (live.Any(c => game.SharesLeft(c.Index) == 0)) yield return SoldOut;
            if (game.CanDiscard && game.Turn > 0) yield return Discard;
        }
        if (game.Companies.Any(c => c.Exists && c.Splits > 0)) yield return Split;
        if (game.LateBuying) yield return LastRounds;
    }
}
