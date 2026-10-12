using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

/// <summary>First-time tips and the guided first game (S12).</summary>
public class TutorialTests
{
    private static Game TutorialGame() =>
        new(Tutorial.Spec, Tutorial.MakeBoard(), [("You", PlayerKind.Human), ("Easy 1", PlayerKind.ComputerEasy), ("Easy 2", PlayerKind.ComputerEasy)],
            GameType.Modern, Tutorial.Rules, Tutorial.Seed);

    [Fact]
    public void Tip_text_follows_the_rule_settings()
    {
        Assert.Contains(ModernRules.BuyLimitPerTurn.ToString(), Tips.Text(Tips.BuyLimit));
        Assert.Contains($"{ModernRules.InterestPercent}%", Tips.Text(Tips.CantAfford));
        Assert.Contains($"{ModernRules.HandSize + 1} squares", Tips.Text(Tips.ExtraSquare));
        Assert.Contains("discard", Tips.Text(Tips.Discard));
        Assert.All(Tips.All, id => Assert.False(string.IsNullOrWhiteSpace(Tips.Text(id))));
    }

    [Fact]
    public void Tips_apply_only_to_a_human_in_a_Modern_game()
    {
        var game = TutorialGame();
        Assert.Contains(Tips.Hand, Tips.Applicable(game));
        Assert.Contains(Tips.SecretGoal, Tips.Applicable(game));
        Assert.Empty(Tips.Applicable(RulesTests.NewGame()));
    }

    [Fact]
    public void The_tutorial_game_is_short_and_the_same_every_time()
    {
        var a = TutorialGame();
        var b = TutorialGame();
        Assert.Equal(Tutorial.Rounds, a.Rounds);
        Assert.Equal(a.HandOf(0), b.HandOf(0));
        Assert.True(a.Board.AllPositions().All(p => a.Board[p] == b.Board[p]));
    }

    [Fact]
    public void The_opening_hand_can_found_a_company()
    {
        var game = TutorialGame();
        Assert.Contains(MovePreview.ForChoices(game), m => m.Kind == MoveKind.Found);
    }

    [Fact]
    public void A_learner_gets_a_merger_square_by_round_7()
    {
        // The seed was picked so the merger steps have something to point at; check it still does after rule changes.
        var game = TutorialGame();
        var ais = new[] { new RuleBasedAi(AiLevel.Medium, 1), new RuleBasedAi(AiLevel.Easy, Tutorial.Seed + 1), new RuleBasedAi(AiLevel.Easy, Tutorial.Seed + 2) };
        int? mergerInHand = null;
        while (game.Phase != GamePhase.Over && game.Turn < 7)
        {
            int me = game.CurrentPlayerIndex;
            if (me == Tutorial.HumanSeat && MovePreview.ForChoices(game).Any(m => m.Kind == MoveKind.Merge)) mergerInHand ??= game.Turn + 1;
            game.PlaceSquare(ais[me].ChooseSquare(game));
            if (game.PendingConversions.Count > 0) game.SettleConversions(game.PendingConversions.ToDictionary(o => o.Player, _ => BigInteger.Zero));
            TradeExecutor.Apply(game, ais[me].PlanTrades(game));
            game.EndTurn();
        }
        Assert.NotNull(mergerInHand);
    }

    [Fact]
    public void Steps_advance_on_actions_and_on_Next()
    {
        var game = TutorialGame();
        var run = new TutorialRun();
        Assert.Equal("hand", run.Current!.Id);
        run.Update(game);
        Assert.Equal("hand", run.Current!.Id);             // waits for a square to be played

        var found = MovePreview.ForChoices(game).First(m => m.Kind == MoveKind.Found).At;
        game.PlaceSquare(found);
        run.Progress.SquaresPlayed++;
        run.Update(game);
        Assert.Equal("buy", run.Current!.Id);

        run.Progress.SharesBought = 1;
        run.Update(game);
        Assert.Equal("grow", run.Current!.Id);
        run.Progress.GrewOwnCompany = true;
        run.Update(game);
        Assert.Equal("majority", run.Current!.Id);
        Assert.True(run.Current.NeedsAcknowledgement);
        run.Acknowledge();
        Assert.Equal("bonus", run.Current!.Id);
        while (!run.Finished) run.Acknowledge();
        Assert.Null(run.Current);
    }

    [Fact]
    public void A_swap_during_the_grow_step_does_not_skip_the_new_hand_step()
    {
        var game = TutorialGame();
        var run = new TutorialRun();
        run.Progress.SquaresPlayed = 1;
        run.Progress.SharesBought = 1;
        run.Update(game);
        Assert.Equal("grow", run.Current!.Id);

        run.Progress.Swaps = 1;                           // the grow step suggests swapping
        run.Progress.GrewOwnCompany = true;
        run.Update(game);
        while (run.Current!.Id != "new-hand") { run.Acknowledge(); run.Update(game); }
        run.Update(game);
        Assert.Equal("new-hand", run.Current!.Id);         // shown, and waits for Next
        run.Progress.NewHands = 1;
        run.Update(game);
        Assert.Equal("new-hand", run.Current!.Id);
    }

    [Fact]
    public void Every_step_that_waits_for_an_action_says_what_to_do()
    {
        Assert.All(Tutorial.Steps.Where(s => !s.NeedsAcknowledgement), s =>
        {
            Assert.False(string.IsNullOrWhiteSpace(s.DoThis));
            Assert.False(string.IsNullOrWhiteSpace(s.Waiting));
        });
    }
}
