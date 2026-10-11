using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

/// <summary>The Market window's order checks (S16).</summary>
public class MarketOrderTests
{
    private static Game ModernWithTwoCompanies()
    {
        var spec = GridSpec.For(GridSize.Medium);
        var board = new Board(spec.Width, spec.Height);
        board[2, 0] = board[8, 0] = Board.Circle;
        var game = new Game(spec, board, [("P0", PlayerKind.Human), ("P1", PlayerKind.Human)], GameType.Modern, new GameRules { Chaos = Chaos.None }, seed: 1);
        game.SetChoicesForTesting(new Pos(2, 1));
        game.PlaceSquare(new Pos(2, 1));        // A at 600
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(8, 1));
        game.PlaceSquare(new Pos(8, 1));        // B at 600
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(0, 10));
        game.PlaceSquare(new Pos(0, 10));       // P0's turn to trade
        return game;
    }

    [Fact]
    public void Sells_pay_for_buys_in_the_same_order()
    {
        var game = ModernWithTwoCompanies();
        game.CurrentPlayer.Money = 0;
        var order = new MarketOrder();
        order.Sells[0] = 3;                     // P0's founder shares: 1,800
        order.Buys[1] = 3;                      // 1,800 of B
        var check = order.Check(game);
        Assert.True(check.Ok, string.Join(" ", check.Problems.Values));
        Assert.Equal(0, check.CashAfter);

        var trades = order.Execute(game);
        Assert.False(trades[0].IsBuy);          // the sale comes first
        Assert.Equal(3, game.Players[0].Shares[1]);
        Assert.Equal(0, game.Players[0].Shares[0]);
    }

    [Fact]
    public void Orders_are_checked_against_cash_allowance_and_holdings()
    {
        var game = ModernWithTwoCompanies();
        var order = new MarketOrder();
        order.Buys[0] = 100;                    // 60,000 against about 6,000 cash
        var check = order.Check(game);
        Assert.Contains("Not enough cash", check.Problems[0]);
        order.Buys[0] = 1_000;
        Assert.Contains("Only 997 shares are left", order.Check(game).Problems[0]);

        game.CurrentPlayer.Money = 10_000_000;
        order.Buys[0] = 60;
        order.Buys[1] = 60;
        check = order.Check(game);
        Assert.Contains("only 100 more", check.Problems[0]);

        order.Clear();
        order.Sells[1] = 1;                     // P0 holds no B
        Assert.Contains("hold only", order.Check(game).Problems[1]);
        Assert.Throws<InvalidOperationException>(() => order.Execute(game));
    }

    [Fact]
    public void Max_takes_the_rest_of_the_order_into_account()
    {
        var game = ModernWithTwoCompanies();
        game.CurrentPlayer.Money = 10_000_000;
        var order = new MarketOrder();
        order.Buys[1] = 70;
        Assert.Equal(30, order.MaxBuy(game, 0));    // the 100-share allowance, less 70

        game.CurrentPlayer.Money = 6_000;
        order.Buys[1] = 5;                          // 3,000
        Assert.Equal(5, order.MaxBuy(game, 0));     // 3,000 left at 600 a share
    }

    [Fact]
    public void Classic_orders_have_no_allowance_or_share_limit()
    {
        var game = RulesTests.NewGame(circles: new Pos(5, 4));
        game.SetChoicesForTesting(new Pos(5, 5));
        game.PlaceSquare(new Pos(5, 5));
        game.CurrentPlayer.Money = 10_000_000;
        var order = new MarketOrder();
        order.Buys[0] = 5_000;
        var check = order.Check(game);
        Assert.True(check.Ok);
        Assert.Equal(-1, check.AllowanceAfter);
    }
}
