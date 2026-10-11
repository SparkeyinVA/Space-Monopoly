using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

public class ModernTests
{
    private static readonly GridSpec Medium = GridSpec.For(GridSize.Medium);

    private static Game NewModern(int players = 3, int seed = 1, params Pos[] circles)
    {
        var board = new Board(Medium.Width, Medium.Height);
        foreach (var c in circles) board[c] = Board.Circle;
        // No events, so the cards can't change the boards these tests set up.
        return new Game(Medium, board, Enumerable.Range(0, players).Select(i => ($"P{i}", PlayerKind.Human)), GameType.Modern,
            new GameRules { Chaos = Chaos.None }, seed);
    }

    private static IReadOnlyList<GameEvent> Play(Game game, Pos p)
    {
        game.SetChoicesForTesting(p);
        return game.PlaceSquare(p);
    }

    [Fact]
    public void Each_player_holds_a_private_hand_and_draws_after_playing()
    {
        var game = NewModern();
        for (int i = 0; i < 3; i++) Assert.Equal(ModernRules.HandSize, game.HandOf(i).Count);
        Assert.Equal(12, Enumerable.Range(0, 3).SelectMany(game.HandOf).Distinct().Count());   // no square in two hands
        Assert.Equal(game.HandOf(0), game.Choices);

        var played = game.Choices[0];
        game.PlaceSquare(played);
        Assert.DoesNotContain(played, game.HandOf(0));
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);
        game.EndTurn();
        Assert.Equal(game.HandOf(1), game.Choices);
    }

    [Fact]
    public void Prospecting_draws_a_new_hand_once_per_game()
    {
        var game = NewModern();
        var old = game.HandOf(0).ToList();
        Assert.True(game.CanProspect);
        game.Prospect();
        Assert.Empty(game.HandOf(0).Intersect(old));
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);
        Assert.Equal(game.HandOf(0), game.Choices);
        Assert.False(game.CanProspect);
        Assert.Throws<InvalidOperationException>(() => game.Prospect());
    }

    [Fact]
    public void Modern_lasts_25_rounds()
    {
        var game = NewModern(players: 3);
        Assert.Equal(ModernRules.Rounds, game.GoesPerPlayer);
        Assert.False(game.GoesReduced);
    }

    [Fact]
    public void Founding_gives_three_shares_and_no_cash_and_growth_pays_no_dividend()
    {
        var game = NewModern(circles: new Pos(5, 4));
        var before = game.Players[0].Money;
        var founded = Assert.Single(Play(game, new Pos(5, 5)).OfType<CompanyFoundedEvent>());
        Assert.Equal(ModernRules.FreeShares, founded.FreeShares);
        Assert.Equal(0, founded.Bonus);
        Assert.Equal(before, game.Players[0].Money);
        Assert.Equal(ModernRules.SharesPerCompany, game.Companies[0].Supply);
        game.EndTurn();

        var grew = Assert.Single(Play(game, new Pos(6, 5)).OfType<CompanyExpandedEvent>());
        Assert.Empty(grew.Dividends);
        Assert.Equal(before, game.Players[0].Money);
    }

    [Fact]
    public void Cash_earns_interest_at_the_start_of_each_turn()
    {
        var game = NewModern(players: 2);
        Assert.Equal(Game.StartingMoney * ModernRules.InterestPercent / 100, game.InterestThisTurn);
        Assert.Equal(Game.StartingMoney * (100 + ModernRules.InterestPercent) / 100, game.Players[0].Money);
        Assert.Equal(Game.StartingMoney, game.Players[1].Money);   // paid on their own turn
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        Assert.Equal(Game.StartingMoney * (100 + ModernRules.InterestPercent) / 100, game.Players[1].Money);

        var classic = RulesTests.NewGame();
        Assert.Equal(Game.StartingMoney, classic.Players[0].Money);
    }

    [Fact]
    public void Buying_is_limited_per_turn_and_by_the_shares_left()
    {
        var game = NewModern(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));             // A at 600 a share; 997 shares left
        Assert.Equal(997, game.SharesLeft(0));
        Assert.Equal(10, game.MaxBuy(0));      // 6,000 / 600
        game.Buy(0, 4);
        Assert.Equal(ModernRules.BuyLimitPerTurn - 4, game.BuyAllowanceLeft);
        Assert.Equal(993, game.SharesLeft(0));
        game.EndTurn();
        Assert.Equal(ModernRules.BuyLimitPerTurn, game.BuyAllowanceLeft);   // resets each turn
    }

    [Fact]
    public void Buy_limit_caps_purchases_when_cash_would_allow_more()
    {
        var game = NewModern(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));
        game.CurrentPlayer.Money = 1_000_000;
        Assert.Equal(ModernRules.BuyLimitPerTurn, game.MaxBuy(0));
        Assert.Contains("only", game.ValidateTrade(0, ModernRules.BuyLimitPerTurn + 1, isBuy: true));
    }

    [Theory]
    [InlineData(new long[] { 30, 20, 10 }, new long[] { 1000, 500, 0 })]  // largest 10x, second 5x
    [InlineData(new long[] { 30, 30, 10 }, new long[] { 750, 750, 0 })]   // tie for largest: both bonuses shared
    [InlineData(new long[] { 30, 10, 10 }, new long[] { 1000, 250, 250 })] // tie for second: second bonus shared
    [InlineData(new long[] { 30, 0, 0 }, new long[] { 1500, 0, 0 })]      // only holder gets both
    [InlineData(new long[] { 0, 0, 0 }, new long[] { 0, 0, 0 })]
    public void Majority_bonuses_follow_the_tie_rules(long[] shares, long[] expected)
    {
        var bonuses = Game.MajorityBonuses(shares.Select((s, i) => (i, new BigInteger(s))), price: 100);
        for (int i = 0; i < shares.Length; i++)
            Assert.Equal(new BigInteger(expected[i]), bonuses.GetValueOrDefault(i));
    }

    [Fact]
    public void Mergers_buy_out_the_absorbed_shares_and_pay_majority_bonuses()
    {
        var game = NewModern(players: 2, circles: [new Pos(2, 0), new Pos(6, 0)]);
        Play(game, new Pos(2, 1));        // P0 founds A with 3 shares
        game.EndTurn();
        Play(game, new Pos(6, 1));        // P1 founds B with 3 shares
        game.EndTurn();
        Play(game, new Pos(3, 1));        // A grows
        game.EndTurn();
        Play(game, new Pos(5, 1));        // B grows; A and B are one square apart
        game.Buy(1, ModernRules.MinSharesForBonus - 3);   // P1 holds 10 B shares, just enough for a bonus
        game.EndTurn();

        long bPrice = game.Companies[1].Value;
        var p0Money = game.Players[0].Money;
        var p1Money = game.Players[1].Money;
        var merger = Assert.Single(Play(game, new Pos(4, 1)).OfType<MergerEvent>());

        Assert.Equal(0, merger.Survivor);       // same size, so the lower-lettered company survives
        Assert.Equal(1, merger.Absorbed);
        Assert.Equal(0, game.Players[1].Shares[1]);
        Assert.Equal(3, game.Players[0].Shares[0]);
        Assert.Equal(0, game.Players[1].Shares[0]);   // no free survivor shares
        // P1, the only qualifying holder of B, is paid for its 10 shares and gets both bonuses.
        Assert.Equal(p1Money + ModernRules.MinSharesForBonus * bPrice + 15 * bPrice, game.Players[1].Money);
        Assert.Equal(p0Money, game.Players[0].Money);
    }

    [Fact]
    public void Holders_of_fewer_than_ten_shares_get_no_bonus()
    {
        var bonuses = Game.MajorityBonuses([(0, 9), (1, 3)], price: 100);
        Assert.Empty(bonuses);
        bonuses = Game.MajorityBonuses([(0, 9), (1, 10)], price: 100);
        Assert.Equal(new BigInteger(1500), bonuses[1]);   // the only qualifying holder gets both
        Assert.False(bonuses.ContainsKey(0));
    }

    [Fact]
    public void One_square_can_be_swapped_each_turn()
    {
        var game = NewModern();
        var swapped = game.HandOf(0)[0];
        game.Swap(swapped);
        Assert.DoesNotContain(swapped, game.HandOf(0));
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);
        Assert.Equal(game.HandOf(0), game.Choices);
        Assert.False(game.CanSwap);
        Assert.Throws<InvalidOperationException>(() => game.Swap(game.HandOf(0)[0]));

        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        Assert.True(game.CanSwap);          // the next player's turn
    }

    [Fact]
    public void Modern_shares_never_split()
    {
        var game = NewModern(players: 1, circles: [new Pos(1, 0), new Pos(3, 0), new Pos(1, 2), new Pos(3, 2)]);
        Play(game, new Pos(2, 1));        // touches no planet: a star
        game.EndTurn();
        Play(game, new Pos(1, 1));        // founds A: 100 + 2 planets + the star = 1,200
        game.EndTurn();
        Play(game, new Pos(3, 1));        // + 100 + 2 planets = 2,300
        game.EndTurn();
        var events = Play(game, new Pos(2, 2));   // + 1,100 = 3,400: Classic would split here
        Assert.DoesNotContain(events, e => e is SplitEvent);
        Assert.Equal(3400, game.Companies[0].Value);
        Assert.Equal(ModernRules.SharesPerCompany, game.Companies[0].Supply);
        Assert.Equal(ModernRules.FreeShares, game.Players[0].Shares[0]);
    }

    [Fact]
    public void Classic_is_unchanged_by_the_Modern_rules()
    {
        var game = RulesTests.NewGame(circles: new Pos(5, 4));
        game.SetChoicesForTesting(new Pos(5, 5));
        var founded = Assert.Single(game.PlaceSquare(new Pos(5, 5)).OfType<CompanyFoundedEvent>());
        Assert.Equal(Game.FreeShares, founded.FreeShares);
        Assert.Equal(founded.SharePrice, founded.Bonus);
        Assert.Equal(-1, game.BuyAllowanceLeft);
        Assert.Equal(-1, game.SharesLeft(0));
        Assert.False(game.CanProspect);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Computer_players_finish_Modern_games(int players)
    {
        for (int seed = 0; seed < 5; seed++)
        {
            var board = new Board(Medium.Width, Medium.Height);
            board.Randomise(new Random(seed), ModernRules.PlanetOdds);
            var kinds = new[] { PlayerKind.ComputerHard, PlayerKind.ComputerMedium, PlayerKind.ComputerEasy };
            var game = new Game(Medium, board, Enumerable.Range(0, players).Select(i => ($"C{i}", kinds[i % 3])), GameType.Modern, new GameRules(), seed);
            var ais = game.Players.Select((p, i) => (RuleBasedAi)AiFactory.Create(p.Kind, new ClaudeCodeSettings(), seed + i)).ToArray();
            while (game.Phase != GamePhase.Over)
            {
                var ai = ais[game.CurrentPlayerIndex];
                game.PlaceSquare(ai.ChooseSquare(game));
                TradeExecutor.Apply(game, ai.PlanTrades(game));
                Assert.All(game.Companies.Where(c => c.Exists), c => Assert.True(game.SharesLeft(c.Index) >= 0));
                Assert.All(game.Players, p => Assert.True(p.Money >= 0));
                game.EndTurn();
            }
            Assert.Equal(game.Rounds, game.Turn);
        }
    }
}
