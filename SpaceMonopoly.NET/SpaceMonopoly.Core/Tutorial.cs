namespace SpaceMonopoly.Core;

/// <summary>What the human has done so far in the tutorial game, for steps that wait for an action (S12).</summary>
public sealed class TutorialProgress
{
    public int SquaresPlayed { get; set; }
    public BigInteger SharesBought { get; set; }
    public int Swaps { get; set; }
    public int NewHands { get; set; }

    /// <summary>True once the human's square has grown a company they hold shares in.</summary>
    public bool GrewOwnCompany { get; set; }
}

/// <summary>
/// One coach step. <paramref name="Done"/> says when an action step is finished; steps that only explain something
/// are finished by the player pressing Next (<paramref name="NeedsAcknowledgement"/>). <paramref name="Target"/> picks out
/// a square on the board to look at, if any. An action step also says plainly what to do (<paramref name="DoThis"/>, shown in
/// bold) and what the game is waiting for (<paramref name="Waiting"/>, shown where Next would be) (F19).
/// </summary>
public sealed record TutorialStep(
    string Id,
    Func<Game, Currency, string> Text,
    Func<Game, TutorialProgress, bool> Done,
    bool NeedsAcknowledgement = false,
    Func<Game, Pos?>? Target = null,
    string? DoThis = null,
    string? Waiting = null);

/// <summary>
/// The guided first game (S12): a fixed board and seed, two Easy opponents and a short Modern game. Step texts are built
/// from the rule settings, so they stay right when the numbers are tuned.
/// </summary>
/// <remarks>
/// The seed was chosen by simulating learners of each level in the human's seat: the opening hand can found a company,
/// a merger square reaches their hand around rounds 4–6, and two to five mergers happen in the 10 rounds.
/// </remarks>
public static class Tutorial
{
    public const int Seed = 19;
    public const int Rounds = 10;
    public const int HumanSeat = 0;

    public static GridSpec Spec => GridSpec.For(GridSize.Medium);

    /// <summary>The tutorial's board: the same for everyone, with planets close together so companies form quickly.</summary>
    public static Board MakeBoard()
    {
        var board = new Board(Spec.Width, Spec.Height);
        board.Randomise(new Random(Seed), ModernRules.PlanetOdds);
        return board;
    }

    public static GameRules Rules => new() { Chaos = Chaos.Calm, RoundsOverride = Rounds };

    /// <summary>The first square in the human's hand that would do this kind of move, to point at.</summary>
    private static Pos? FirstSquare(Game game, MoveKind kind) =>
        MovePreview.ForChoices(game).FirstOrDefault(m => m.Kind == kind)?.At;

    public static IReadOnlyList<TutorialStep> Steps { get; } =
    [
        new("hand",
            (g, c) => $"Welcome! The flashing squares are your hand: {ModernRules.HandSize} squares only you can see, also listed under Your hand on the right. " +
                      $"Each is marked with what it would do; point at one for details. A + square founds a new company next to a planet, " +
                      $"and the founder gets {ModernRules.FreeShares} free shares.",
            (g, p) => g.CompaniesFounded[HumanSeat] > 0 || p.SquaresPlayed > 0,
            Target: g => FirstSquare(g, MoveKind.Found) ?? g.Choices.FirstOrDefault(),
            DoThis: "Play a + square to found your first company: click it on the board, or press Play beside it in Your hand.",
            Waiting: "Waiting for you to play a square"),
        new("buy",
            (g, c) => $"After playing a square you can trade. You can buy up to {ModernRules.BuyLimitPerTurn} shares a turn, and cash you keep " +
                      $"earns {ModernRules.InterestPercent}% interest each turn.",
            (g, p) => p.SharesBought > 0,
            DoThis: "Press Market (or M), type a number in a company's Buy box, then press Confirm trades.",
            Waiting: "Waiting for you to buy shares"),
        new("grow",
            (g, c) => $"Each square a company grows by raises its share price by {c.Format(Game.SquareValue)}, plus {c.Format(Game.CircleBonus)} for each planet " +
                      "next to it, so growing a company you hold makes you richer. Press End Turn when you've finished trading; the others then play.",
            (g, p) => p.GrewOwnCompany,
            Target: g => MovePreview.ForChoices(g).FirstOrDefault(m => m.Kind == MoveKind.Grow && g.Players[HumanSeat].Shares[m.Company] > 0)?.At,
            DoThis: "On a later turn, play a square marked with the letter of a company you hold (Swap a square if you have none).",
            Waiting: "Waiting for you to grow your company"),
        new("majority",
            (g, c) => $"When two companies meet (an M square), the bigger takes over the smaller. The smaller one's two largest holders, with at least " +
                      $"{ModernRules.MinSharesForBonus} shares, get {ModernRules.LargestHolderBonus}× and {ModernRules.SecondHolderBonus}× its share price as a bonus " +
                      "(doubled for each split the company has had). So buy into companies that are about to be taken over, and become their largest holder.",
            (g, p) => false, NeedsAcknowledgement: true,
            Target: g => FirstSquare(g, MoveKind.Merge)),
        new("bonus",
            (g, c) => "Hold a merger square until you lead the company it would take over, then play it to collect the bonus. " +
                      "The Advisor button shows what a strong player would do, if you're not sure.",
            (g, p) => g.BonusesCollected[HumanSeat] > 0, NeedsAcknowledgement: true,
            Target: g => FirstSquare(g, MoveKind.Merge)),
        new("new-hand",
            (g, c) => $"Stuck with poor squares? Swap one a turn, or take a New hand once per game. A square you don't play within " +
                      $"{ModernRules.SquareExpiryTurns} turns is replaced automatically.",
            (g, p) => p.Swaps > 0 || p.NewHands > 0, NeedsAcknowledgement: true),
        new("end",
            (g, c) => $"A real Modern game lasts {ModernRules.Rounds} rounds. At the end, companies pay their largest holders a final payout and secret goals " +
                      "are revealed. The highest net worth wins. Watch the side panel for your secret goal and next round's event card. " +
                      "You're ready: finish this game or start your own from Game > New Game.",
            (g, p) => false, NeedsAcknowledgement: true),
    ];
}

/// <summary>A tutorial in progress: which step the player is on.</summary>
public sealed class TutorialRun(int startAt = 0)
{
    public TutorialProgress Progress { get; } = new();
    public int StepIndex { get; private set; } = startAt;
    public bool Finished => StepIndex >= Tutorial.Steps.Count;
    public TutorialStep? Current => Finished ? null : Tutorial.Steps[StepIndex];

    /// <summary>Moves past action steps that are done. Returns true if the step changed.</summary>
    public bool Update(Game game)
    {
        bool moved = false;
        while (Current is { } step && step.Done(game, Progress))
        {
            StepIndex++;
            moved = true;
        }
        return moved;
    }

    /// <summary>The player pressed Next on a step that explains something.</summary>
    public void Acknowledge()
    {
        if (!Finished) StepIndex++;
    }
}
