using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

/// <summary>Modern's special squares (S11).</summary>
public class SpecialSquareTests
{
    private static readonly GridSpec Medium = GridSpec.For(GridSize.Medium);

    private static Game Modern(Board board) =>
        new(Medium, board, [("P0", PlayerKind.Human), ("P1", PlayerKind.Human)], GameType.Modern, new GameRules { Chaos = Chaos.None }, seed: 1);

    private static IReadOnlyList<GameEvent> Play(Game game, Pos p)
    {
        game.SetChoicesForTesting(p);
        return game.PlaceSquare(p);
    }

    [Fact]
    public void Wormhole_ends_count_as_next_to_each_other()
    {
        var board = new Board(Medium.Width, Medium.Height);
        board[2, 2] = Board.Wormhole;
        board[18, 14] = Board.Wormhole;
        Assert.Contains(new Pos(18, 13), board.Neighbours(new Pos(2, 1)));   // next to one end, so next to the other's squares
        Assert.DoesNotContain(new Pos(18, 13), board.Neighbours(new Pos(5, 5)));
    }

    [Fact]
    public void Companies_merge_through_a_wormhole()
    {
        var board = new Board(Medium.Width, Medium.Height);
        board[2, 2] = Board.Wormhole;
        board[18, 14] = Board.Wormhole;
        board[1, 0] = board[18, 11] = Board.Circle;
        var game = Modern(board);
        Play(game, new Pos(1, 1));        // founds A by the planet at (1,0)
        game.EndTurn();
        Play(game, new Pos(18, 12));      // founds B by the planet at (18,11)
        game.EndTurn();
        Play(game, new Pos(18, 13));      // B grows next to the far wormhole end
        game.EndTurn();
        // (2,1) touches A and the near wormhole end, so it also touches B's square at (18,13).
        Assert.Single(Play(game, new Pos(2, 1)).OfType<MergerEvent>());
    }

    [Fact]
    public void A_nebula_doubles_the_planet_bonus()
    {
        var board = new Board(Medium.Width, Medium.Height);
        board[5, 4] = Board.Circle;
        board[6, 5] = Board.Nebula;
        var game = Modern(board);
        var founded = Assert.Single(Play(game, new Pos(5, 5)).OfType<CompanyFoundedEvent>());
        Assert.Equal(Game.SquareValue + 2 * Game.CircleBonus, founded.SharePrice);
    }

    [Fact]
    public void Squares_next_to_a_black_hole_are_never_dealt()
    {
        var board = new Board(Medium.Width, Medium.Height);
        board[10, 10] = Board.BlackHole;
        var game = Modern(board);
        var blocked = new[] { new Pos(10, 9), new Pos(9, 10), new Pos(11, 10), new Pos(10, 11) };
        for (int i = 0; i < 30; i++)
        {
            Assert.Empty(game.Choices.Intersect(blocked));
            game.PlaceSquare(game.Choices[0]);
            game.EndTurn();
        }
    }

    [Fact]
    public void Special_squares_survive_a_new_game_and_the_daily_board_has_them()
    {
        var board = Replay.DailyBoard(new DateTime(2026, 10, 11));
        Assert.Equal(1, board.CountOf(Board.Nebula));
        Assert.Equal(1, board.CountOf(Board.BlackHole));
        Assert.Equal(2, board.CountOf(Board.Wormhole));
        var game = Modern(board);
        Assert.Equal(2, game.Board.CountOf(Board.Wormhole));
    }
}
