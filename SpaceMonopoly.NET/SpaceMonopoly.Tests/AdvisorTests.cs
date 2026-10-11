using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

/// <summary>The Advisor (S14).</summary>
public class AdvisorTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SpaceMonopolyAdvisor-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    private static Game MidGame(GameType type, int seed)
    {
        var spec = GridSpec.For(GridSize.Medium);
        var board = new Board(spec.Width, spec.Height);
        board.Randomise(new Random(seed), type == GameType.Modern ? ModernRules.PlanetOdds : Board.CircleOdds);
        var game = new Game(spec, board, [("You", PlayerKind.Human), ("Hard", PlayerKind.ComputerHard)], type, new GameRules(), seed);
        var hard = new RuleBasedAi(AiLevel.Hard, seed);
        for (int i = 0; i < 16; i++)
        {
            game.PlaceSquare(hard.ChooseSquare(game));
            TradeExecutor.Apply(game, hard.PlanTrades(game));
            game.EndTurn();
        }
        return game;
    }

    [Theory]
    [InlineData(GameType.Modern, 1)]
    [InlineData(GameType.Modern, 2)]
    [InlineData(GameType.Classic, 3)]
    public void The_suggestion_is_what_Hard_would_play_and_names_where_the_gain_comes_from(GameType type, int seed)
    {
        var game = MidGame(type, seed);
        var advice = Advisor.Suggest(game);

        var hard = new RuleBasedAi(AiLevel.Hard, Advisor.SeedFor(game));
        Assert.Equal(hard.ChooseSquare(game.Clone(), mayChangeHand: false), advice.Square);
        Assert.Matches("merger bonus|jumps in price|its growth is yours|grows a company|found a company|merges companies|nothing better", advice.Reason);
    }

    [Fact]
    public void While_trading_it_suggests_trades_only()
    {
        var game = MidGame(GameType.Modern, 4);
        game.PlaceSquare(game.Choices[0]);
        var advice = Advisor.Suggest(game);
        Assert.Null(advice.Square);
        Assert.False(string.IsNullOrWhiteSpace(advice.Reason));
    }

    [Fact]
    public void Advisor_use_is_recorded_in_the_high_scores_and_old_files_still_load()
    {
        var storage = new Storage(_folder);
        var table = HighScoreTable.CreateDefault();
        table.Add(GridSize.Medium, new HighScoreEntry("You", 500_000, PlayerKind.Human, GameType.Modern, DateTime.Today, AdvisorUses: 7));
        storage.SaveHighScores(table);
        Assert.Equal(7, storage.LoadHighScores().For(GridSize.Medium, modern: true)[0].AdvisorUses);

        File.WriteAllText(Path.Combine(_folder, "highscores.json"),
            """{ "Tables": { "Small": [ { "Name": "Old", "Wealth": 5, "Kind": "Human", "GameType": "Classic", "Date": "2026-01-01T00:00:00" } ] } }""");
        Assert.Equal(0, storage.LoadHighScores().For(GridSize.Small)[0].AdvisorUses);
    }
}
