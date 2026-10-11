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

    public static readonly IReadOnlyList<string> All = [Hand, MergerSquare, BuyLimit, CantAfford, EventComing, SecretGoal, Split];

    public static string Text(string id, Currency? currency = null)
    {
        var c = currency ?? Currency.Default;
        return id switch
        {
            Hand => $"Your hand: you hold {ModernRules.HandSize} squares that only you can see. Play one each turn and you draw a new one. " +
                    "You can swap one square a turn, and take a whole new hand once per game.",
            MergerSquare => $"You hold a merger square (marked M). The company taken over pays its two largest holders a bonus of " +
                            $"{ModernRules.LargestHolderBonus}× and {ModernRules.SecondHolderBonus}× its share price (at least {ModernRules.MinSharesForBonus} shares " +
                            "to qualify). Try to lead that company before you play it.",
            BuyLimit => $"You've bought the most shares you can this turn ({ModernRules.BuyLimitPerTurn}). You can still sell; the limit resets next turn.",
            CantAfford => $"You can't afford a share right now. Cash earns {ModernRules.InterestPercent}% interest each turn, and selling shares raises cash.",
            EventComing => "Next round's event card is shown in the side panel. Plan for it: for example, avoid the target of a Pirate Raid.",
            SecretGoal => "You have a secret goal (shown in the side panel). Meet it by the end of the game for a cash reward; the others' goals are revealed then.",
            Split => $"A company has split: its share price halved and everyone's shares doubled, so your holding is worth the same. " +
                     $"Splits happen at {ModernRules.FirstSplitSquares} squares and {c.Format(ModernRules.FirstSplitPrice)}, then {ModernRules.SecondSplitSquares} " +
                     $"squares and {c.Format(ModernRules.SecondSplitPrice)}, and make bonuses bigger.",
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
        }
        else
        {
            if (game.BuyAllowanceLeft == 0) yield return BuyLimit;
            var live = game.Companies.Where(c => c.Exists).ToList();
            if (live.Count > 0 && live.All(c => c.Value > game.CurrentPlayer.Money)) yield return CantAfford;
        }
        if (game.Companies.Any(c => c.Exists && c.Splits > 0)) yield return Split;
    }
}
