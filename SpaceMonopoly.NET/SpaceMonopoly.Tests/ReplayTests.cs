using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

/// <summary>End-of-game statistics, awards, achievements and the daily challenge (S10).</summary>
public class ReplayTests
{
    private static Game PlayOut(GameType type, int seed)
    {
        var spec = GridSpec.For(GridSize.Medium);
        var board = new Board(spec.Width, spec.Height);
        board.Randomise(new Random(seed), type == GameType.Modern ? ModernRules.PlanetOdds : Board.CircleOdds);
        var game = new Game(spec, board, [("You", PlayerKind.Human), ("Hard", PlayerKind.ComputerHard), ("Medium", PlayerKind.ComputerMedium)], type, new GameRules(), seed);
        var ais = new[] { new RuleBasedAi(AiLevel.Hard, seed), new RuleBasedAi(AiLevel.Hard, seed + 1), new RuleBasedAi(AiLevel.Medium, seed + 2) };
        while (game.Phase != GamePhase.Over)
        {
            var ai = ais[game.CurrentPlayerIndex];
            game.PlaceSquare(ai.ChooseSquare(game));
            if (game.PendingConversions.Count > 0)
                game.SettleConversions(game.PendingConversions.ToDictionary(o => o.Player, o => ais[o.Player].ChooseConversion(game, o)));
            TradeExecutor.Apply(game, ai.PlanTrades(game));
            game.EndTurn();
        }
        return game;
    }

    [Fact]
    public void Net_worth_is_recorded_every_round_and_at_the_end()
    {
        var game = PlayOut(GameType.Modern, 3);
        // The start, the end of every round, and the final net worths after payouts.
        Assert.Equal(game.Rounds + 2, game.NetWorthHistory.Count);
        Assert.Equal(Enumerable.Range(0, 3).Select(game.NetWorth), game.NetWorthHistory[^1]);
        Assert.InRange(game.LastAtHalfway(), 0, 2);
    }

    [Fact]
    public void Awards_and_achievements_come_from_the_game()
    {
        var game = PlayOut(GameType.Modern, 5);
        var awards = Replay.Awards(game, Currency.Default);
        Assert.Contains(awards, a => a.Title == "Best company");
        if (game.BiggestMerger is not null) Assert.Contains(awards, a => a.Title == "Biggest merger");

        bool youWon = game.Standings()[0].Player == 0;
        var earned = Replay.Earned(game, 0, advisorUses: 0, daily: true).Select(a => a.Id).ToList();
        Assert.Contains("daily", earned);
        Assert.Equal(youWon, earned.Contains("first-win"));
        Assert.Equal(youWon, earned.Contains("giant-killer"));
        Assert.Equal(game.CompaniesFounded[0] >= 3, earned.Contains("tycoon"));
    }

    [Fact]
    public void The_daily_challenge_is_the_same_all_day_and_different_tomorrow()
    {
        var today = new DateTime(2026, 10, 11);
        Assert.Equal(Replay.DailySeed(today), Replay.DailySeed(today.AddHours(20)));
        Assert.NotEqual(Replay.DailySeed(today), Replay.DailySeed(today.AddDays(1)));

        var a = Replay.DailyBoard(today);
        var b = Replay.DailyBoard(today);
        Assert.True(a.AllPositions().All(p => a[p] == b[p]));
    }
}
