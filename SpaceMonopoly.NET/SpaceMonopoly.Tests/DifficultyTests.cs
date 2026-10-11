using System.Text.Json;
using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

/// <summary>Adaptive difficulty and "Claude plays key turns only" (S9).</summary>
public class DifficultyTests
{
    [Fact]
    public void The_ladder_moves_up_after_a_win_and_down_after_a_loss_and_stops_at_the_ends()
    {
        Assert.Equal(1, AdaptiveDifficulty.Next(0, humanWon: true));
        Assert.Equal(0, AdaptiveDifficulty.Next(0, humanWon: false));
        int top = AdaptiveDifficulty.Steps.Count - 1;
        Assert.Equal(top, AdaptiveDifficulty.Next(top, humanWon: true));
        Assert.Equal(top - 1, AdaptiveDifficulty.Next(top, humanWon: false));
        Assert.Equal("Medium", AdaptiveDifficulty.At(0).Label);
        Assert.Equal("Hard", AdaptiveDifficulty.At(top).Label);
    }

    [Fact]
    public void Normal_steps_differ_by_how_often_they_settle_for_the_second_best_square()
    {
        var easier = AdaptiveDifficulty.CreatePlayer(1);
        var harder = AdaptiveDifficulty.CreatePlayer(3);
        Assert.Equal(AiLevel.Normal, easier.Level);
        Assert.True(easier.SecondBestChance > harder.SecondBestChance);
        Assert.Equal(AiLevel.Hard, AdaptiveDifficulty.CreatePlayer(4).Level);
    }

    private sealed class CountingRunner : IClaudeRunner
    {
        public int Calls { get; private set; }

        public Task<JsonElement> RunAsync(string systemPrompt, string prompt, string jsonSchema, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(JsonDocument.Parse("""{"choice":1,"trades":[],"comment":"","plan":""}""").RootElement.Clone());
        }
    }

    [Fact]
    public async Task With_key_turns_only_Claude_is_asked_only_when_a_merger_or_new_company_is_possible()
    {
        var game = RulesTests.NewGame(circles: new Pos(5, 4));
        var runner = new CountingRunner();
        var ai = new ClaudeCodeAi(runner, new RuleBasedAi(AiLevel.Hard, 1)) { KeyTurnsOnly = true };

        game.SetChoicesForTesting(new Pos(0, 0), new Pos(15, 11));        // only stars on offer
        await ai.ChooseSquareAsync(game, CancellationToken.None);
        Assert.Equal(0, runner.Calls);

        game.SetChoicesForTesting(new Pos(0, 0), new Pos(5, 5));          // (5,5) would found a company
        await ai.ChooseSquareAsync(game, CancellationToken.None);
        Assert.Equal(1, runner.Calls);
    }
}
