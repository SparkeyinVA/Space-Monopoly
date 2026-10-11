using System.Text.Json;
using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

/// <summary>The Claude player's instructions and position (S13).</summary>
public class ClaudePromptTests
{
    private sealed class RecordingRunner(params string[] answers) : IClaudeRunner
    {
        private int _next;
        public List<(string System, string Prompt)> Calls { get; } = [];

        public Task<JsonElement> RunAsync(string systemPrompt, string prompt, string jsonSchema, CancellationToken cancellationToken)
        {
            Calls.Add((systemPrompt, prompt));
            return Task.FromResult(JsonDocument.Parse(answers[Math.Min(_next++, answers.Length - 1)]).RootElement.Clone());
        }
    }

    private static Game Modern(int players = 2, params Pos[] circles)
    {
        var spec = GridSpec.For(GridSize.Medium);
        var board = new Board(spec.Width, spec.Height);
        foreach (var c in circles) board[c] = Board.Circle;
        return new Game(spec, board, Enumerable.Range(0, players).Select(i => ($"P{i}", PlayerKind.Human)), GameType.Modern,
            new GameRules { Chaos = Chaos.None }, seed: 3);
    }

    private static readonly string[] ClassicOnly =
        ["5 free shares plus cash", "cash dividend", "same number of shares in the survivor", "split two-for-one", "cash earns nothing"];

    private static readonly string[] ModernOnly =
        ["hand", "interest", "majority", "never more than twice", "secret goal", "prospect"];

    [Fact]
    public void Each_mode_gets_only_its_own_rules()
    {
        string modern = ClaudeCodeAi.BuildSystemPrompt(Modern());
        string classic = ClaudeCodeAi.BuildSystemPrompt(RulesTests.NewGame());

        foreach (var phrase in ClassicOnly)
        {
            Assert.Contains(phrase, classic, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(phrase, modern, StringComparison.OrdinalIgnoreCase);
        }
        foreach (var phrase in ModernOnly)
        {
            Assert.Contains(phrase, modern, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(phrase, classic, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void The_position_includes_net_worth_changes_and_who_leads_each_company()
    {
        var game = Modern(circles: new Pos(5, 4));
        game.SetChoicesForTesting(new Pos(5, 5));
        game.PlaceSquare(new Pos(5, 5));      // P0 founds A
        game.Buy(0, 9);                       // 12 shares: qualifies
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(6, 5), new Pos(0, 0));

        string prompt = ClaudeCodeAi.BuildPrompt(game);
        Assert.Contains("Net worth change: P0 +", prompt);
        Assert.Contains("Bonus now goes to: P0", prompt);
        Assert.Contains($"You can buy {ModernRules.BuyLimitPerTurn} more shares this turn", prompt);
    }

    [Fact]
    public async Task The_plan_is_shown_back_next_turn()
    {
        var game = Modern();
        var runner = new RecordingRunner("""{"choice":1,"trades":[],"comment":"Hi.","plan":"Grow Atlantis next."}""");
        var ai = new ClaudeCodeAi(runner, new RuleBasedAi(AiLevel.Hard, 1));

        game.PlaceSquare(await ai.ChooseSquareAsync(game, CancellationToken.None));
        game.EndTurn();
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        await ai.ChooseSquareAsync(game, CancellationToken.None);

        Assert.DoesNotContain("Your plan last turn", runner.Calls[0].Prompt);
        Assert.Contains("Your plan last turn: Grow Atlantis next.", runner.Calls[1].Prompt);
    }

    [Fact]
    public async Task A_prospect_answer_is_applied_and_Claude_is_asked_again()
    {
        var game = Modern();
        var oldHand = game.HandOf(0).ToList();
        var runner = new RecordingRunner(
            """{"choice":1,"trades":[],"comment":"","plan":"","prospect":true}""",
            """{"choice":2,"trades":[],"comment":"New hand.","plan":""}""");
        var ai = new ClaudeCodeAi(runner, new RuleBasedAi(AiLevel.Hard, 1));

        var square = await ai.ChooseSquareAsync(game, CancellationToken.None);
        Assert.True(game.HasProspected(0));
        Assert.Equal(2, runner.Calls.Count);
        Assert.Contains("You prospected", runner.Calls[1].Prompt);
        Assert.Equal(game.HandOf(0)[1], square);
        Assert.Empty(game.HandOf(0).Intersect(oldHand));
    }

    [Fact]
    public async Task A_swap_answer_replaces_one_square()
    {
        var game = Modern();
        var swapped = game.HandOf(0)[2];
        var runner = new RecordingRunner(
            """{"choice":1,"trades":[],"comment":"","plan":"","swap":3}""",
            """{"choice":1,"trades":[],"comment":"","plan":""}""");
        var ai = new ClaudeCodeAi(runner, new RuleBasedAi(AiLevel.Hard, 1));

        await ai.ChooseSquareAsync(game, CancellationToken.None);
        Assert.DoesNotContain(swapped, game.HandOf(0));
        Assert.False(game.CanSwap);
        Assert.Equal(2, runner.Calls.Count);
    }

    [Fact]
    public void A_personality_adds_its_paragraph()
    {
        Assert.Contains("RAIDER", ClaudeCodeAi.BuildSystemPrompt(Modern(), Personality.Raider));
        Assert.DoesNotContain("PERSONALITY", ClaudeCodeAi.BuildSystemPrompt(Modern()));
    }

    [Fact]
    public void The_prompt_shows_what_Hard_would_play_and_advice_changes_nothing()
    {
        var game = Modern(circles: new Pos(5, 4));
        var hand = game.HandOf(0).ToList();
        var money = game.Players[0].Money;

        var advice = Advisor.Suggest(game);
        Assert.InRange(advice.Choice, 1, game.Choices.Count);
        Assert.False(string.IsNullOrWhiteSpace(advice.Reason));
        Assert.Contains($"The built-in Hard strategy would play square {advice.Choice}", ClaudeCodeAi.BuildPrompt(game));

        Assert.Equal(hand, game.HandOf(0));
        Assert.Equal(money, game.Players[0].Money);
        Assert.True(game.CanSwap && game.CanProspect);
    }
}
