using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

/// <summary>Modern splits by size and price, the share limit and starting cash (S19).</summary>
public class SplitTests
{
    private static readonly GridSpec Medium = GridSpec.For(GridSize.Medium);

    /// <summary>A one-player Modern game (no events) where A is founded at (1,1) and then grown along row 1.</summary>
    private static Game WithCompany(params Pos[] circles)
    {
        var board = new Board(Medium.Width, Medium.Height);
        foreach (var c in circles) board[c] = Board.Circle;
        var game = new Game(Medium, board, [("P0", PlayerKind.Human)], GameType.Modern, new GameRules { Chaos = Chaos.None }, seed: 1);
        Play(game, new Pos(1, 1));
        return game;
    }

    private static IReadOnlyList<GameEvent> Play(Game game, Pos p)
    {
        if (game.Phase == GamePhase.Trading) game.EndTurn();
        game.SetChoicesForTesting(p);
        return game.PlaceSquare(p);
    }

    [Fact]
    public void Modern_starts_with_more_cash_and_a_50_share_buy_limit()
    {
        var game = WithCompany(new Pos(1, 0));
        Assert.Equal(50, ModernRules.BuyLimitPerTurn);
        Assert.Equal(ModernRules.StartingMoney + ModernRules.StartingMoney * ModernRules.InterestPercent / 100, game.Players[0].Money);
        Assert.Equal(ModernRules.SharesPerCompany, game.Companies[0].Supply);
        Assert.Equal(100, ModernRules.SharesPerCompany);
    }

    [Fact]
    public void A_cheap_company_does_not_split_however_big_it_gets_below_the_price()
    {
        var game = WithCompany(new Pos(1, 0));                 // 600, then +100 a square
        for (int x = 2; x <= 6; x++) Play(game, new Pos(x, 1));   // 6 squares, 1,100
        Assert.Equal(0, game.Companies[0].Splits);
    }

    [Fact]
    public void An_expensive_small_company_does_not_split_below_the_size()
    {
        // Founded between four planets: 100 + 4 x 500 = 2,100, but only 1 square.
        var game = WithCompany(new Pos(1, 0), new Pos(0, 1), new Pos(2, 0), new Pos(1, 2));
        Assert.True(game.Companies[0].Value >= ModernRules.FirstSplitPrice);
        Assert.Equal(0, game.Companies[0].Splits);
    }

    [Fact]
    public void Splits_happen_twice_at_most_and_double_the_shares_and_the_limit()
    {
        // A planet above every square of row 1, so each square adds 600.
        var circles = Enumerable.Range(1, 14).Select(x => new Pos(x, 0)).ToArray();
        var game = WithCompany(circles);
        var shares = game.Players[0].Shares[0];
        for (int x = 2; x <= 5; x++) Play(game, new Pos(x, 1));   // 5 squares
        Assert.Equal(1, game.Companies[0].Splits);
        Assert.Equal(2 * shares, game.Players[0].Shares[0]);
        Assert.Equal(2 * ModernRules.SharesPerCompany, game.Companies[0].Supply);

        for (int x = 6; x <= 14; x++) Play(game, new Pos(x, 1));  // 14 squares
        Assert.Equal(2, game.Companies[0].Splits);
        Assert.Equal(4 * shares, game.Players[0].Shares[0]);
        Assert.Equal(4 * ModernRules.SharesPerCompany, game.Companies[0].Supply);
        Assert.Null(ModernRules.NextSplit(game.Companies[0].Splits));   // a blue chip: no third split
    }

    [Fact]
    public void Splits_never_reverse()
    {
        var circles = Enumerable.Range(1, 6).Select(x => new Pos(x, 0)).ToArray();
        var game = WithCompany(circles);
        for (int x = 2; x <= 5; x++) Play(game, new Pos(x, 1));
        Assert.Equal(1, game.Companies[0].Splits);
        game.Board[5, 1] = Board.Empty;                       // as if an Asteroid Strike took a square
        Play(game, new Pos(10, 10));
        Assert.Equal(1, game.Companies[0].Splits);
        Assert.Equal(2 * ModernRules.SharesPerCompany, game.Companies[0].Supply);
    }
}
