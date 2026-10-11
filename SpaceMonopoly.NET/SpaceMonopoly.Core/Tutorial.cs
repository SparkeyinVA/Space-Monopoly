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
/// a square on the board to look at, if any.
/// </summary>
public sealed record TutorialStep(
    string Id,
    Func<Game, Currency, string> Text,
    Func<Game, TutorialProgress, bool> Done,
    bool NeedsAcknowledgement = false,
    Func<Game, Pos?>? Target = null);

/// <summary>
/// The guided first game (S12): a fixed board and seed, two Easy opponents and a short Modern game. Step texts are built
/// from the rule settings, so they stay right when the numbers are tuned.
/// </summary>
/// <remarks>The step script below is a DRAFT until the design reviewer and the user approve the final one.</remarks>
public static class Tutorial
{
    public const int Seed = 20261010;
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
            (g, c) => $"Welcome! These flashing squares are your hand: {ModernRules.HandSize} squares only you can see. Each is marked with what it would do, " +
                      "and the list on the right says the same. Point at a square for details, then play any square to start.",
            (g, p) => p.SquaresPlayed > 0),
        new("found",
            (g, c) => $"A + square founds a new company next to a planet or star. The founder gets {ModernRules.FreeShares} free shares. Found a company now " +
                      "(if you have no + square, Swap one square for a new one).",
            (g, p) => g.CompaniesFounded[HumanSeat] > 0,
            Target: g => FirstSquare(g, MoveKind.Found)),
        new("buy",
            (g, c) => $"After playing a square you can trade. Open the Market (button or M) and buy shares in a company: up to {ModernRules.BuyLimitPerTurn} " +
                      $"a turn. Cash you keep earns {ModernRules.InterestPercent}% interest each turn.",
            (g, p) => p.SharesBought > 0),
        new("grow",
            (g, c) => $"Each square a company grows by raises its share price by {c.Format(Game.SquareValue)}, plus {c.Format(Game.CircleBonus)} for each planet " +
                      "next to it. Grow a company you hold shares in (a square marked with its letter).",
            (g, p) => p.GrewOwnCompany,
            Target: g => MovePreview.ForChoices(g).FirstOrDefault(m => m.Kind == MoveKind.Grow && g.Players[HumanSeat].Shares[m.Company] > 0)?.At),
        new("majority",
            (g, c) => $"When two companies meet (an M square), the bigger takes over the smaller. The smaller one's two largest holders, with at least " +
                      $"{ModernRules.MinSharesForBonus} shares, get {ModernRules.LargestHolderBonus}× and {ModernRules.SecondHolderBonus}× its share price as a bonus. " +
                      "So buy into companies that are about to be taken over, and become their largest holder.",
            (g, p) => false, NeedsAcknowledgement: true),
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
                      "are revealed. The highest net worth wins. You're ready: finish this game or start your own from Game > New Game.",
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
