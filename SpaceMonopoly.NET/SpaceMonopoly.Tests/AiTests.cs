using System.Text.Json;
using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

public class AiTests
{
    private static Game RandomGame(GridSize size, GameType type, int seed, params PlayerKind[] kinds)
    {
        var spec = GridSpec.For(size);
        var board = new Board(spec.Width, spec.Height);
        board.Randomise(new Random(seed));
        var players = kinds.Select((k, i) => ($"{k}{i}", k));
        return new Game(spec, board, players, type, new GameRules(), seed);
    }

    /// <summary>Plays a whole game with rule-based players and checks the books balance every turn.</summary>
    private static Game PlayOut(Game game, int seed)
    {
        var ais = game.Players.Select((p, i) => (RuleBasedAi)AiFactory.Create(p.Kind, new ClaudeCodeSettings(), seed + i)).ToArray();
        int guard = 0;
        while (game.Phase != GamePhase.Over)
        {
            var ai = ais[game.CurrentPlayerIndex];
            game.PlaceSquare(ai.ChooseSquare(game));
            TradeExecutor.Apply(game, ai.PlanTrades(game));

            foreach (var p in game.Players)
            {
                Assert.True(p.Money >= 0, $"{p.Name} has negative money");
                Assert.All(p.Shares, s => Assert.True(s >= 0));
            }
            foreach (var c in game.Companies)
            {
                if (c.Exists) Assert.InRange(c.Value, 100, Game.SplitThreshold - 1);
                else Assert.Equal(0, game.Board.CompanySize(c.Index));
            }

            game.EndTurn();
            Assert.True(++guard < 10_000, "game didn't finish");
        }
        return game;
    }

    [Theory]
    [InlineData(GridSize.Small, GameType.Classic)]
    [InlineData(GridSize.Medium, GameType.Classic)]
    [InlineData(GridSize.Large, GameType.Classic)]
    [InlineData(GridSize.Small, GameType.Tournament)]
    [InlineData(GridSize.Large, GameType.Tournament)]
    public void Computer_players_complete_whole_games(GridSize size, GameType type)
    {
        for (int seed = 0; seed < 5; seed++)
        {
            var game = PlayOut(RandomGame(size, type, seed, PlayerKind.ComputerEasy, PlayerKind.ComputerMedium, PlayerKind.ComputerHard), seed);
            Assert.Equal(GamePhase.Over, game.Phase);
            // Either every go was played or the board ran out of room for new companies first.
            Assert.Equal(game.Rounds, game.Turn);
            Assert.True(game.Rounds == game.GoesPerPlayer || game.NoMoreCompanies);
        }
    }

    [Fact]
    public void Five_computer_players_can_share_a_board()
    {
        var game = PlayOut(RandomGame(GridSize.Medium, GameType.Classic, 11,
            PlayerKind.ComputerHard, PlayerKind.ComputerHard, PlayerKind.ComputerMedium, PlayerKind.ComputerEasy, PlayerKind.ComputerEasy), 11);
        Assert.Equal(GamePhase.Over, game.Phase);
    }

    [Theory]
    [InlineData(PlayerKind.ComputerHard, PlayerKind.ComputerEasy)]
    [InlineData(PlayerKind.ComputerMedium, PlayerKind.ComputerEasy)]
    [InlineData(PlayerKind.ComputerHard, PlayerKind.ComputerMedium)]
    public void Stronger_level_usually_beats_weaker_level(PlayerKind strong, PlayerKind weak)
    {
        int wins = 0;
        const int games = 30;
        for (int seed = 0; seed < games; seed++)
        {
            // Alternate who goes first.
            var kinds = seed % 2 == 0 ? new[] { strong, weak } : new[] { weak, strong };
            var game = PlayOut(RandomGame(GridSize.Small, GameType.Classic, 100 + seed, kinds), 100 + seed);
            if (game.Standings()[0].Kind == strong) wins++;
        }
        Assert.True(wins >= games * 0.65, $"{strong} won only {wins} of {games} against {weak}");
    }

    // ------------------------------------------------------------------ Claude player

    private sealed class FakeRunner(Func<string, JsonElement> answer) : IClaudeRunner
    {
        public string? LastPrompt { get; private set; }

        public Task<JsonElement> RunAsync(string systemPrompt, string prompt, string jsonSchema, CancellationToken cancellationToken)
        {
            LastPrompt = prompt;
            return Task.FromResult(answer(prompt));
        }
    }

    private static JsonElement Json(string s) => JsonDocument.Parse(s).RootElement.Clone();

    [Fact]
    public async Task Claude_player_uses_the_chosen_square_and_trades()
    {
        var game = RulesTests.NewGame(circles: new Pos(5, 4));
        game.SetChoicesForTesting(new Pos(0, 0), new Pos(5, 5));
        var runner = new FakeRunner(_ => Json("""{"choice":2,"trades":[{"action":"buy","company":"A","quantity":0}],"comment":"Founding by the planet."}"""));
        var ai = new ClaudeCodeAi(runner, new RuleBasedAi(AiLevel.Hard, 1));

        var square = await ai.ChooseSquareAsync(game, CancellationToken.None);
        Assert.Equal(new Pos(5, 5), square);
        game.PlaceSquare(square);
        var trades = TradeExecutor.Apply(game, await ai.PlanTradesAsync(game, CancellationToken.None));

        Assert.Equal("Founding by the planet.", ai.LastComment);
        var buy = Assert.Single(trades);
        Assert.Equal(11, buy.Quantity);
        Assert.Contains("Your options:", runner.LastPrompt);
        Assert.Contains("founded Atlantis Ltd.", runner.LastPrompt);
    }

    [Fact]
    public async Task Claude_player_falls_back_to_built_in_strategy_on_failure()
    {
        var game = RulesTests.NewGame(circles: new Pos(5, 4));
        game.SetChoicesForTesting(new Pos(0, 0), new Pos(5, 5));
        var runner = new FakeRunner(_ => throw new ClaudeCodeException("Not logged in."));
        var ai = new ClaudeCodeAi(runner, new RuleBasedAi(AiLevel.Hard, 1));

        var square = await ai.ChooseSquareAsync(game, CancellationToken.None);

        Assert.Contains(square, game.Choices);
        Assert.Contains("Not logged in.", ai.LastComment);
    }

    [Fact]
    public async Task Claude_player_rejects_a_square_that_was_not_offered()
    {
        var game = RulesTests.NewGame();
        game.SetChoicesForTesting(new Pos(0, 0), new Pos(3, 3));
        var ai = new ClaudeCodeAi(new FakeRunner(_ => Json("""{"choice":5,"trades":[],"comment":""}""")), new RuleBasedAi(AiLevel.Hard, 1));

        var square = await ai.ChooseSquareAsync(game, CancellationToken.None);

        Assert.Contains(square, game.Choices);
        Assert.Contains("wasn't offered", ai.LastComment);
    }

    [Fact]
    public void Claude_Code_output_is_parsed_from_structured_output()
    {
        const string output = """{"type":"result","is_error":false,"result":"ignored","structured_output":{"choice":3,"trades":[],"comment":"hi"}}""";
        var answer = ClaudeCodeRunner.ParseResult(output, "", 0);
        Assert.Equal(3, answer.GetProperty("choice").GetInt32());
    }

    [Fact]
    public void Claude_Code_errors_are_reported()
    {
        Assert.Throws<ClaudeCodeException>(() => ClaudeCodeRunner.ParseResult("""{"is_error":true,"result":"Not logged in"}""", "", 1));
        Assert.Throws<ClaudeCodeException>(() => ClaudeCodeRunner.ParseResult("", "command not found", 1));
    }
}
