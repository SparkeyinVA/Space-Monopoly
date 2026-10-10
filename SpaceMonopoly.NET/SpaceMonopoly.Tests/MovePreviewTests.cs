using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

public class MovePreviewTests
{
    private static Game Offer(Game game, params Pos[] squares)
    {
        game.SetChoicesForTesting(squares);
        return game;
    }

    [Fact]
    public void Each_kind_of_move_is_previewed_without_changing_the_game()
    {
        // A planet at (5,4): (5,5) founds a company, (0,0) is a plain star.
        var game = Offer(RulesTests.NewGame(circles: new Pos(5, 4)), new Pos(5, 5), new Pos(0, 0));
        var previews = MovePreview.ForChoices(game);

        Assert.Equal(MoveKind.Found, previews[0].Kind);
        Assert.Equal("+", previews[0].Label);
        Assert.Equal(600, previews[0].PriceAfter);
        Assert.Equal(MoveKind.Star, previews[1].Kind);
        Assert.Equal("★", previews[1].Label);

        // Nothing was played on the real game.
        Assert.Equal(Board.Empty, game.Board[5, 5]);
        Assert.Equal(GamePhase.ChoosingSquare, game.Phase);
        Assert.Equal(2, game.Choices.Count);
    }

    [Fact]
    public void Growth_and_mergers_show_the_company_and_who_gains()
    {
        var game = RulesTests.NewGame(players: 2, circles: [new Pos(2, 0), new Pos(6, 0)]);
        Offer(game, new Pos(2, 1)).PlaceSquare(new Pos(2, 1));   // P0 founds A
        game.EndTurn();
        Offer(game, new Pos(6, 1)).PlaceSquare(new Pos(6, 1));   // P1 founds B
        game.EndTurn();

        Offer(game, new Pos(3, 1), new Pos(4, 1));
        var grow = MovePreview.For(game, new Pos(3, 1));
        Assert.Equal(MoveKind.Grow, grow.Kind);
        Assert.Equal("A", grow.Label);
        Assert.Equal(100, grow.PriceRise);
        Assert.True(grow.NetWorthChange[0] > 0);       // P0 holds A
        Assert.Equal(0, grow.NetWorthChange[1]);       // P1 doesn't

        game.SetChoicesForTesting(new Pos(3, 1));
        game.PlaceSquare(new Pos(3, 1));
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(5, 1));
        Assert.Equal(MoveKind.Grow, MovePreview.For(game, new Pos(5, 1)).Kind);   // only B is next to (5,1)

        game.PlaceSquare(new Pos(5, 1));
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(4, 1));
        var merge = MovePreview.For(game, new Pos(4, 1));
        Assert.Equal(MoveKind.Merge, merge.Kind);
        Assert.Equal("M", merge.Label);
        Assert.Contains("takes over", merge.Summary(Currency.Default));
    }

    [Fact]
    public void Previews_and_copies_do_not_change_what_the_real_game_deals_next()
    {
        Game Make()
        {
            var spec = GridSpec.For(GridSize.Small);
            var board = new Board(spec.Width, spec.Height);
            board.Randomise(new Random(4));
            return new Game(spec, board, [("A", PlayerKind.Human), ("B", PlayerKind.Human)], GameType.Classic, new GameRules(), seed: 4);
        }

        var plain = Make();
        var previewed = Make();
        for (int turn = 0; turn < 10; turn++)
        {
            MovePreview.ForChoices(previewed);
            previewed.Clone().Clone();
            Assert.Equal(plain.Choices, previewed.Choices);

            plain.PlaceSquare(plain.Choices[0]);
            previewed.PlaceSquare(previewed.Choices[0]);
            plain.EndTurn();
            previewed.EndTurn();
        }
    }

    [Fact]
    public void With_hidden_details_only_your_own_gain_is_shown()
    {
        var game = RulesTests.NewGame(players: 2, rules: new GameRules { HideDetails = true }, circles: [new Pos(2, 0)]);
        game.SetChoicesForTesting(new Pos(2, 1));
        game.PlaceSquare(new Pos(2, 1));        // P0 founds A
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(3, 1));
        game.PlaceSquare(new Pos(3, 1));        // P1 grows it
        game.Buy(0, 1);
        game.EndTurn();

        game.SetChoicesForTesting(new Pos(4, 1));
        string details = MovePreview.For(game, new Pos(4, 1)).Details(game, Currency.Default);
        Assert.Contains("You +", details);
        Assert.DoesNotContain("P1", details);
    }
}
