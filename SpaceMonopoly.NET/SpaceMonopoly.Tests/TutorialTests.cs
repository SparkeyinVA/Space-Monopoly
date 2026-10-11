using SpaceMonopoly.Core;

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
        Assert.Equal("buy", run.Current!.Id);              // playing a + square also completed "found"

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
}
