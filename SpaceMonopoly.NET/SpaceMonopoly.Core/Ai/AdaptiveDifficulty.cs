namespace SpaceMonopoly.Core.Ai;

/// <summary>One step of the adaptive difficulty ladder: the level computer players play at, and Normal's settling chance.</summary>
public sealed record DifficultyStep(string Label, PlayerKind Kind, double SecondBestChance);

/// <summary>
/// Adaptive difficulty (S9): after each game the human wins, the computer players move one step up the ladder;
/// after each loss, one step down. Normal is split into finer steps by how often it settles for its second-best square.
/// </summary>
public static class AdaptiveDifficulty
{
    public static readonly IReadOnlyList<DifficultyStep> Steps =
    [
        new("Medium", PlayerKind.ComputerMedium, 0),
        new("Normal (easier)", PlayerKind.ComputerNormal, 0.5),
        new("Normal", PlayerKind.ComputerNormal, 0.3),
        new("Normal (harder)", PlayerKind.ComputerNormal, 0.1),
        new("Hard", PlayerKind.ComputerHard, 0),
    ];

    public static DifficultyStep At(int step) => Steps[Math.Clamp(step, 0, Steps.Count - 1)];

    /// <summary>The step for the next game: one up after a win, one down after a loss.</summary>
    public static int Next(int step, bool humanWon) => Math.Clamp(step + (humanWon ? 1 : -1), 0, Steps.Count - 1);

    /// <summary>A computer player for this step (Claude players aren't changed by the ladder).</summary>
    public static RuleBasedAi CreatePlayer(int step, int? seed = null, Personality personality = Personality.None)
    {
        var s = At(step);
        var level = s.Kind switch
        {
            PlayerKind.ComputerMedium => AiLevel.Medium,
            PlayerKind.ComputerNormal => AiLevel.Normal,
            _ => AiLevel.Hard,
        };
        return new RuleBasedAi(level, seed, personality) { SecondBestChance = s.SecondBestChance };
    }
}
