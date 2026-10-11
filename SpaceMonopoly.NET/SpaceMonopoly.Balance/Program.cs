using System.Globalization;
using System.Numerics;
using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

// Plays computer-vs-computer games and reports the balance measures from CLAUDE.md.
//
//   dotnet run -c Release --project SpaceMonopoly.Balance                 the standard Modern report
//   dotnet run -c Release --project SpaceMonopoly.Balance -- --classic yes  the same for Classic (slow: long games)
//   dotnet run -c Release --project SpaceMonopoly.Balance -- --type Modern --size Medium --players Hard,Medium,Easy --games 200
//
// --type Modern|Classic|Tournament   --size Small|Medium|Large   --players a comma list of Easy, Medium and Hard
// --chaos Calm|Standard|Wild|None (Modern events, default Standard)   --games N (default 200)   --seed N (first seed, default 1)

var options = Options.Parse(args);
if (options.Custom)
{
    var results = Simulator.Run(options.Type, options.Size, options.Players, options.Games, options.Seed, options.Chaos);
    Report.Print(results);
    if (options.Players.Distinct().Count() > 1) Report.PrintWinRates(results, "Wins by level");
    return;
}

var type = options.Classic ? GameType.Classic : GameType.Modern;
Console.WriteLine($"Space Monopoly balance report: {type}, medium grid, {(type == GameType.Modern ? options.Chaos + " events, " : "")}" +
                  $"{options.Games} games per setup, seeds from {options.Seed}");
Console.WriteLine();
Console.WriteLine("== Hard, Medium, Easy (headline) ==");
var mixed = Simulator.Run(type, GridSize.Medium, [AiLevel.Hard, AiLevel.Medium, AiLevel.Easy], options.Games, options.Seed, options.Chaos);
Report.Print(mixed);
Report.PrintWinRates(mixed, "Wins by level");
Console.WriteLine();
Console.WriteLine("== Hard, Hard, Hard ==");
Report.Print(Simulator.Run(type, GridSize.Medium, [AiLevel.Hard, AiLevel.Hard, AiLevel.Hard], options.Games, options.Seed, options.Chaos));
Console.WriteLine();
Report.PrintWinRates(Simulator.Run(type, GridSize.Medium, [AiLevel.Hard, AiLevel.Easy], options.Games, options.Seed, options.Chaos), "Hard v Easy, one-on-one");

internal sealed record Options(bool Custom, bool Classic, GameType Type, GridSize Size, AiLevel[] Players, int Games, int Seed, Chaos Chaos)
{
    public static Options Parse(string[] args)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (int i = 0; i + 1 < args.Length; i += 2) map[args[i].TrimStart('-')] = args[i + 1];
        string Get(string key, string fallback) => map.TryGetValue(key, out var v) ? v : fallback;

        return new Options(
            Custom: map.ContainsKey("type") || map.ContainsKey("players") || map.ContainsKey("size"),
            Classic: map.ContainsKey("classic"),
            Type: Enum.Parse<GameType>(Get("type", "Modern"), ignoreCase: true),
            Size: Enum.Parse<GridSize>(Get("size", "Medium"), ignoreCase: true),
            Players: Get("players", "Hard,Hard,Hard").Split(',').Select(p => Enum.Parse<AiLevel>(p.Trim(), ignoreCase: true)).ToArray(),
            Games: int.Parse(Get("games", "200"), CultureInfo.InvariantCulture),
            Seed: int.Parse(Get("seed", "1"), CultureInfo.InvariantCulture),
            Chaos: Enum.Parse<Chaos>(Get("chaos", "Standard"), ignoreCase: true));
    }
}

/// <summary>What happened in one game.</summary>
internal sealed record GameResult(
    AiLevel[] Levels,
    BigInteger[] Final,
    int Winner,
    int LastAtHalfway,
    int Turns,
    int DeadTurns,
    int NothingForYouTurns,
    int Mergers,
    int MergersWithoutBonus,
    double CashShareSum,
    int CashShareCount,
    int LeadChanges,
    bool EndedEarly);

internal static class Simulator
{
    public static List<GameResult> Run(GameType type, GridSize size, AiLevel[] levels, int games, int firstSeed, Chaos chaos)
    {
        var results = new GameResult[games];
        Parallel.For(0, games, g => results[g] = Play(type, size, levels, firstSeed + g, chaos));
        return [.. results];
    }

    private static GameResult Play(GameType type, GridSize size, AiLevel[] levels, int seed, Chaos chaos)
    {
        var spec = GridSpec.For(size);
        var board = new Board(spec.Width, spec.Height);
        board.Randomise(new Random(seed), type == GameType.Modern ? ModernRules.PlanetOdds : Board.CircleOdds);

        // Rotate the seats so no level always goes first.
        var seats = levels.Select((l, i) => levels[(i + seed) % levels.Length]).ToArray();
        var game = new Game(spec, board, seats.Select((l, i) => ($"{l} {i + 1}", ToKind(l))), type, new GameRules { Chaos = chaos }, seed);
        var ais = seats.Select((l, i) => new RuleBasedAi(l, seed * 31 + i)).ToArray();

        int turns = 0, dead = 0, nothingForYou = 0, mergers = 0, unpaid = 0, leadChanges = 0, leader = -1, lastAtHalfway = -1;
        double cashShare = 0;
        int cashCount = 0;
        int halfway = Math.Max(1, game.GoesPerPlayer / 2);

        while (game.Phase != GamePhase.Over)
        {
            // A dead turn: every square on offer only places a star. "Nothing for you": no square raises your own net worth.
            // Both are measured at the start of the turn, before any swap or prospect.
            var previews = MovePreview.ForChoices(game);
            if (previews.All(m => m.Kind == MoveKind.Star)) dead++;
            if (previews.All(m => m.NetWorthChange[game.CurrentPlayerIndex] <= 0)) nothingForYou++;

            var ai = ais[game.CurrentPlayerIndex];
            foreach (var merger in game.PlaceSquare(ai.ChooseSquare(game)).OfType<MergerEvent>())
            {
                mergers++;
                if (merger.Rows.All(r => r.Bonus == 0)) unpaid++;
            }
            TradeExecutor.Apply(game, ai.PlanTrades(game));
            turns++;

            var worth = game.NetWorth(game.CurrentPlayerIndex);
            if (worth > 0)
            {
                cashShare += (double)game.CurrentPlayer.Money / (double)worth;
                cashCount++;
            }

            bool roundEnds = game.CurrentPlayerIndex == game.Players.Count - 1;
            game.EndTurn();
            if (!roundEnds) continue;

            int now = Leader(game);
            if (leader >= 0 && now != leader) leadChanges++;
            leader = now;
            if (game.Turn == halfway) lastAtHalfway = Last(game);
        }

        var final = Enumerable.Range(0, game.Players.Count).Select(game.NetWorth).ToArray();
        return new GameResult(seats, final, Leader(game), lastAtHalfway, turns, dead, nothingForYou, mergers, unpaid, cashShare, cashCount, leadChanges,
            game.NoMoreCompanies && game.Turn < game.GoesPerPlayer);
    }

    private static int Leader(Game game) =>
        Enumerable.Range(0, game.Players.Count).OrderByDescending(game.NetWorth).First();

    private static int Last(Game game) =>
        Enumerable.Range(0, game.Players.Count).OrderBy(game.NetWorth).First();

    private static PlayerKind ToKind(AiLevel level) => level switch
    {
        AiLevel.Easy => PlayerKind.ComputerEasy,
        AiLevel.Medium => PlayerKind.ComputerMedium,
        _ => PlayerKind.ComputerHard,
    };
}

internal static class Report
{
    public static void Print(List<GameResult> results)
    {
        var ratios = results.Where(r => r.Final.Length > 1).Select(r =>
        {
            var sorted = r.Final.OrderByDescending(x => x).ToArray();
            return sorted[1] > 0 ? (double)sorted[0] / (double)sorted[1] : double.PositiveInfinity;
        }).OrderBy(x => x).ToList();
        var lastPlace = results.Where(r => r.Final.Length > 1).Select(r =>
        {
            var sorted = r.Final.OrderByDescending(x => x).ToArray();
            return sorted[^1] > 0 ? (double)sorted[0] / (double)sorted[^1] : double.PositiveInfinity;
        }).OrderBy(x => x).ToList();
        var top = results.Select(r => r.Final.Max()).OrderBy(x => x).ToList();
        int turns = results.Sum(r => r.Turns);

        Row("Turns with no useful square", Percent(results.Sum(r => r.DeadTurns), turns), "under 2%");
        Row("Turns where no square helps you", Percent(results.Sum(r => r.NothingForYouTurns), turns), "");
        Row("Mergers per game; share with no bonus paid", $"{(double)results.Sum(r => r.Mergers) / results.Count:0.0}; " +
            Percent(results.Sum(r => r.MergersWithoutBonus), results.Sum(r => r.Mergers)), "");
        if (ratios.Count > 0)
        {
            Row("Winner / second place, median", $"{Quantile(ratios, 0.5):0.00}x", "1.3-1.5x");
            Row("Winner / second place, 90th percentile", $"{Quantile(ratios, 0.9):0.00}x", "about 2.5-3x");
            Row("Winner / last place, median", $"{Quantile(lastPlace, 0.5):0.00}x", "");
            int withHalfway = results.Count(r => r.LastAtHalfway >= 0 && r.Final.Length > 2);
            if (withHalfway > 0)
                Row("Last at halfway goes on to win", Percent(results.Count(r => r.Final.Length > 2 && r.LastAtHalfway == r.Winner), withHalfway), "15-25%");
            Row("Lead changes per game, average", $"{results.Average(r => r.LeadChanges):0.0}", "");
        }
        Row("Cash as a share of net worth after trading", Percent(results.Sum(r => r.CashShareSum), results.Sum(r => r.CashShareCount)), "10-30%");
        Row("Largest final net worth, median", Currency.Default.Format(top[top.Count / 2]), "");
        Row("Largest final net worth, highest", Currency.Default.Format(top[^1]), "under about 1 million");
        Row("Turns per game, average", $"{(double)turns / results.Count:0}", "");
        Row("Games ended early (no room for companies)", Percent(results.Count(r => r.EndedEarly), results.Count), "");
    }

    public static void PrintWinRates(List<GameResult> results, string title)
    {
        var levels = results[0].Levels.Distinct().ToArray();
        var wins = levels.ToDictionary(l => l, l => results.Count(r => r.Levels[r.Winner] == l));
        Row(title, string.Join(", ", levels.Select(l => $"{l} {Percent(wins[l], results.Count)}")),
            levels.Length == 2 && levels.Contains(AiLevel.Easy) ? "Hard 80-90%" : "");
    }

    private static void Row(string label, string value, string target) =>
        Console.WriteLine($"  {label,-44} {value,-24} {(target.Length > 0 ? "target " + target : "")}");

    private static string Percent(double part, double whole) => whole == 0 ? "-" : $"{100 * part / whole:0.0}%";

    private static double Quantile(List<double> sorted, double q)
    {
        if (sorted.Count == 0) return double.NaN;
        double pos = q * (sorted.Count - 1);
        int lo = (int)Math.Floor(pos), hi = (int)Math.Ceiling(pos);
        return sorted[lo] + (sorted[hi] - sorted[lo]) * (pos - lo);
    }
}
