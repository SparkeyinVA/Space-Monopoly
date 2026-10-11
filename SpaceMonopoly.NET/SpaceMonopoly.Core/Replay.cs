namespace SpaceMonopoly.Core;

/// <summary>An award on the end-of-game screen (S10).</summary>
public sealed record Award(string Title, string Text);

/// <summary>Something a player can earn once and keep (S10).</summary>
public sealed record Achievement(string Id, string Title, string Description);

/// <summary>The end-of-game awards, achievements and the daily challenge (S10).</summary>
public static class Replay
{
    public static readonly IReadOnlyList<Achievement> Achievements =
    [
        new("first-win", "First win", "Win a game."),
        new("modern-win", "Modern master", "Win a Modern game."),
        new("giant-killer", "Giant killer", "Win against a Hard computer player."),
        new("on-my-own", "On my own", "Win against a Normal or Hard computer player without asking the Advisor."),
        new("raider", "Raider", "Collect a majority bonus at a merger."),
        new("tycoon", "Tycoon", "Found 3 companies in one game."),
        new("comeback", "Comeback", "Win after being in last place at the halfway round."),
        new("half-million", "Half a million", "Finish a Modern game worth ₲500,000 or more."),
        new("daily", "Daily challenger", "Finish a Daily Challenge."),
    ];

    private static string Ordinal(int n) => n switch { 1 => "1st", 2 => "2nd", 3 => "3rd", _ => $"{n}th" };

    public static List<Award> Awards(Game game, Currency currency)
    {
        var awards = new List<Award>();
        string Name(int p) => game.Players[p].Name;

        if (game.BiggestMerger is { } m)
            awards.Add(new("Biggest merger", $"{Name(m.Player)} was paid {currency.Format(m.Amount)} when {Company.Names[m.Survivor]} " +
                                             $"took over {Company.Names[m.Absorbed]} in round {m.Round}."));

        var world = game.WorldFor(game.Standings()[0].Player);
        if (world.Companies.Where(c => c.Exists).OrderByDescending(c => (double)c.Value * world.Board.CompanySize(c.Index)).FirstOrDefault() is { } best)
            awards.Add(new("Best company", $"{best.Name}: {world.Board.CompanySize(best.Index)} squares at {currency.Format(best.Value)} a share."));

        // Biggest comeback: the furthest climb from a player's worst place at the end of a round to their final place.
        var history = game.NetWorthHistory;
        if (game.Players.Count > 1 && history.Count > 2)
        {
            int Rank(IReadOnlyList<BigInteger> worth, int p) => 1 + Enumerable.Range(0, worth.Count).Count(i => worth[i] > worth[p]);
            var final = history[^1];
            var climb = Enumerable.Range(0, game.Players.Count)
                .Select(p => (Player: p, Worst: history.Skip(1).Max(h => Rank(h, p)), Final: Rank(final, p)))
                .OrderByDescending(t => t.Worst - t.Final).First();
            if (climb.Worst > climb.Final)
                awards.Add(new("Biggest comeback", $"{Name(climb.Player)} climbed from {Ordinal(climb.Worst)} to {Ordinal(climb.Final)}."));
        }

        int founder = Enumerable.Range(0, game.Players.Count).OrderByDescending(p => game.CompaniesFounded[p]).First();
        if (game.CompaniesFounded[founder] > 0)
            awards.Add(new("Top founder", $"{Name(founder)} founded {game.CompaniesFounded[founder]} compan{(game.CompaniesFounded[founder] == 1 ? "y" : "ies")}."));

        foreach (var g in game.GameEndEvents.OfType<SecretGoalEvent>().Where(g => g.Reward > 0))
            awards.Add(new("Secret goal", $"{Name(g.Player)}: {SecretGoals.Description(g.Goal)}"));
        return awards;
    }

    /// <summary>The achievements a human player earned in this finished game.</summary>
    public static List<Achievement> Earned(Game game, int player, int advisorUses, bool daily)
    {
        var ids = new List<string>();
        bool won = game.Standings()[0].Player == player;
        var opponents = game.Players.Where((_, i) => i != player).Select(p => p.Kind).ToList();
        if (won) ids.Add("first-win");
        if (won && game.IsModern) ids.Add("modern-win");
        if (won && opponents.Contains(PlayerKind.ComputerHard)) ids.Add("giant-killer");
        if (won && advisorUses == 0 && opponents.Any(k => k is PlayerKind.ComputerHard or PlayerKind.ComputerNormal)) ids.Add("on-my-own");
        if (player < game.BonusesCollected.Count && game.BonusesCollected[player] > 0) ids.Add("raider");
        if (player < game.CompaniesFounded.Count && game.CompaniesFounded[player] >= 3) ids.Add("tycoon");
        if (won && game.Players.Count > 1 && game.LastAtHalfway() == player) ids.Add("comeback");
        if (game.IsModern && game.NetWorth(player) >= 500_000) ids.Add("half-million");
        if (daily) ids.Add("daily");
        return Achievements.Where(a => ids.Contains(a.Id)).ToList();
    }

    /// <summary>The daily challenge's seed: the same for everyone on the same day.</summary>
    public static int DailySeed(DateTime date) => date.Year * 10000 + date.Month * 100 + date.Day;

    /// <summary>The daily challenge's board: Modern, medium grid, from the day's seed.</summary>
    public static Board DailyBoard(DateTime date)
    {
        var spec = GridSpec.For(GridSize.Medium);
        var board = new Board(spec.Width, spec.Height);
        var rng = new Random(DailySeed(date));
        board.Randomise(rng, ModernRules.PlanetOdds);
        board.PlaceSpecials(rng);
        return board;
    }
}
