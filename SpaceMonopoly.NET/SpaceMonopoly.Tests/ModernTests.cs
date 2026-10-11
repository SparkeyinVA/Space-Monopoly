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
        long start = ModernRules.StartingMoney;
        Assert.Equal(start * ModernRules.InterestPercent / 100, game.InterestThisTurn);
        Assert.Equal(start * (100 + ModernRules.InterestPercent) / 100, game.Players[0].Money);
        Assert.Equal(start, game.Players[1].Money);   // paid on their own turn
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        Assert.Equal(start * (100 + ModernRules.InterestPercent) / 100, game.Players[1].Money);

        var classic = RulesTests.NewGame();
        Assert.Equal(Game.StartingMoney, classic.Players[0].Money);
    }

    [Fact]
    public void Buying_is_limited_per_turn_and_by_the_shares_left()
    {
        var game = NewModern(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));             // A at 600 a share; the founder holds 3
        Assert.Equal(ModernRules.SharesPerCompany - 3, game.SharesLeft(0));
        var afford = game.CurrentPlayer.Money / 600;
        Assert.Equal(BigInteger.Min(afford, ModernRules.BuyLimitPerTurn), game.MaxBuy(0));
        game.Buy(0, 4);
        Assert.Equal(ModernRules.BuyLimitPerTurn - 4, game.BuyAllowanceLeft);
        Assert.Equal(ModernRules.SharesPerCompany - 7, game.SharesLeft(0));
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
        long aPrice = game.Companies[0].Value;
        var p0Money = game.Players[0].Money;
        var p1Money = game.Players[1].Money;
        var merger = Assert.Single(Play(game, new Pos(4, 1)).OfType<MergerEvent>());

        Assert.Equal(0, merger.Survivor);       // same size, so the lower-lettered company survives
        // The survivor's price rises by half the absorbed price, plus the square itself (no planet beside it).
        Assert.Equal(aPrice + bPrice / 2 + Game.SquareValue, game.Companies[0].Value);
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

    [Fact]
    public void A_player_alone_in_last_place_holds_an_extra_square()
    {
        var game = NewModern(players: 2);
        game.Players[1].Money = 100;
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        Assert.Equal(ModernRules.HandSize + 1, game.HandOf(1).Count);
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);
    }

    [Fact]
    public void Every_player_gets_a_different_secret_goal_while_there_are_enough()
    {
        var game = NewModern(players: 6);
        var goals = Enumerable.Range(0, 6).Select(i => game.GoalOf(i)!.Value).ToList();
        Assert.Equal(6, goals.Distinct().Count());
        Assert.Null(RulesTests.NewGame().GoalOf(0));
    }

    [Fact]
    public void Founder_goal_pays_for_each_company_still_in_play()
    {
        var game = NewModern(players: 1, circles: [new Pos(2, 0), new Pos(8, 0)]);
        Play(game, new Pos(2, 1));
        game.EndTurn();
        Play(game, new Pos(8, 1));
        game.EndNow();
        var goal = Assert.Single(game.GameEndEvents.OfType<SecretGoalEvent>());
        if (goal.Goal == SecretGoal.Founder) Assert.Equal(2 * SecretGoals.FounderRewardEach, goal.Reward);
    }

    /// <summary>A game where P0 founded A (3 shares) and A has grown to <paramref name="squares"/> squares; it's then P0's turn to trade.</summary>
    private static Game WithCompanyOf(int squares, int players = 3)
    {
        var game = NewModern(players, circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));
        for (int x = 6; x < 5 + squares; x++)
        {
            game.EndTurn();
            Play(game, new Pos(x, 5));
        }
        while (game.CurrentPlayerIndex != 0)
        {
            game.EndTurn();
            game.PlaceSquare(game.Choices[0]);
        }
        return game;
    }

    private static void Hold(Game game, int player, int shares) => game.Players[player].Shares[0] = shares;

    [Fact]
    public void Companies_of_four_squares_pay_half_bonuses_to_their_two_largest_holders_at_the_end()
    {
        var game = WithCompanyOf(4);
        Hold(game, 0, 12);
        Hold(game, 1, 10);
        Hold(game, 2, 5);                                // too few to qualify
        long price = game.Companies[0].Value;
        var before = game.Players.Select(p => p.Money).ToArray();

        game.EndNow();
        var payout = Assert.Single(game.GameEndEvents.OfType<FinalPayoutEvent>());
        Assert.Equal(0, payout.Company);
        var goals = game.GameEndEvents.OfType<SecretGoalEvent>().ToDictionary(g => g.Player, g => g.Reward);
        Assert.Equal(before[0] + 5 * price + goals[0], game.Players[0].Money);
        Assert.Equal(before[1] + 5 * price / 2 + goals[1], game.Players[1].Money);
        Assert.Equal(before[2] + goals[2], game.Players[2].Money);

        var paidAgain = game.Players.Select(p => p.Money).ToArray();
        game.EndNow();                                   // paid once only
        Assert.Equal(paidAgain, game.Players.Select(p => p.Money).ToArray());
    }

    [Fact]
    public void Small_companies_pay_no_final_payout()
    {
        var game = WithCompanyOf(3);
        Hold(game, 0, 12);
        game.EndNow();
        Assert.Empty(game.GameEndEvents.OfType<FinalPayoutEvent>());
    }

    [Fact]
    public void A_sole_holder_gets_only_the_first_final_payout()
    {
        var game = WithCompanyOf(4);
        Hold(game, 0, 10);
        long price = game.Companies[0].Value;
        game.EndNow();
        var payout = Assert.Single(game.GameEndEvents.OfType<FinalPayoutEvent>());
        Assert.Equal(new BigInteger(5 * price), Assert.Single(payout.Payouts).Amount);
    }

    [Fact]
    public void Shares_bought_in_the_last_two_rounds_do_not_count_for_the_final_ranking()
    {
        var game = WithCompanyOf(4, players: 2);
        Hold(game, 1, 12);
        while (!game.LateBuying || game.CurrentPlayerIndex != 0)
        {
            if (game.Phase == GamePhase.ChoosingSquare) game.PlaceSquare(game.Choices[0]);
            game.EndTurn();
        }
        game.PlaceSquare(game.Choices[0]);
        game.CurrentPlayer.Money = 1_000_000;
        var before = game.Players[0].Shares[0];          // the founder's shares (doubled by any splits)
        game.Buy(0, 20);                                 // bought too late to count
        Assert.Equal(before, game.SharesForFinalRanking(0, 0));
        game.EndNow();
        // P1's 12 shares stay first: P0's late purchase doesn't lift P0 above them.
        var payout = Assert.Single(game.GameEndEvents.OfType<FinalPayoutEvent>());
        Assert.Equal(1, payout.Payouts.MaxBy(p => p.Amount)!.Player);
    }

    [Fact]
    public void Hard_buys_enough_to_take_first_place()
    {
        // Late in the game, when cash has few rounds of interest left to earn.
        var game = WithCompanyOf(4, players: 2);
        while (game.Turn < game.Rounds - ModernRules.LateBuyRounds - 2 || game.CurrentPlayerIndex != 0)
        {
            if (game.Phase == GamePhase.ChoosingSquare) game.PlaceSquare(game.Choices[0]);
            game.EndTurn();
        }
        game.PlaceSquare(game.Choices[0]);
        Assert.True(game.Companies[0].Exists && game.Board.CompanySize(0) >= ModernRules.FinalPayoutMinSquares);
        Hold(game, 0, 3);
        Hold(game, 1, 12);
        game.CurrentPlayer.Money = 50 * game.Companies[0].Value;

        TradeExecutor.Apply(game, new RuleBasedAi(AiLevel.Hard, 1).PlanTrades(game));
        Assert.True(game.Players[0].Shares[0] > game.Players[1].Shares[0],
            $"Hard holds {game.Players[0].Shares[0]} against {game.Players[1].Shares[0]}");
    }

    private static Game NewModernWithEvents(int players = 3) =>
        new(Medium, new Board(Medium.Width, Medium.Height), Enumerable.Range(0, players).Select(i => ($"P{i}", PlayerKind.Human)),
            GameType.Modern, new GameRules { Chaos = Chaos.Standard }, seed: 2);

    private static void PlayRound(Game game)
    {
        do
        {
            game.PlaceSquare(game.Choices[0]);
            game.EndTurn();
        } while (game.CurrentPlayerIndex != 0 && game.Phase != GamePhase.Over);
    }

    [Fact]
    public void No_events_in_the_first_two_rounds_and_round_3s_card_is_announced()
    {
        var game = NewModernWithEvents();
        var announced = game.NextEvent;
        Assert.Null(game.CurrentEvent);
        Assert.Equal(EventCards.FirstEventRound, game.NextEventRound);
        PlayRound(game);
        Assert.Null(game.CurrentEvent);
        Assert.Equal(announced, game.NextEvent);
        PlayRound(game);
        Assert.Equal(announced, game.CurrentEvent);
        Assert.Equal(4, game.NextEventRound);
    }

    [Fact]
    public void Cards_that_could_not_affect_anything_are_set_aside_before_being_announced()
    {
        var game = NewModernWithEvents();
        // No companies exist, so Solar Flare and Pirate Raid can't do anything; Quiet Quarter is never set aside.
        game.SetDeckForTesting(EventCard.SolarFlare, EventCard.PirateRaid, EventCard.QuietQuarter, EventCard.SolarFlare);
        Assert.Equal(EventCard.QuietQuarter, game.NextEvent);
    }

    [Fact]
    public void Galactic_grant_pays_a_tenth_of_the_average_only_to_a_player_well_behind()
    {
        var game = NewModernWithEvents(players: 2);
        Assert.Null(game.GrantRecipient());          // everyone level
        game.Players[1].Money = 1_000;
        var (player, amount) = game.GrantRecipient()!.Value;
        Assert.Equal(1, player);
        var average = (game.NetWorth(0) + game.NetWorth(1)) / 2;
        Assert.Equal(average / 10 / 100 * 100, amount);
        Assert.Contains("about", game.DescribeEvent(EventCard.GalacticGrant));
    }

    [Fact]
    public void The_extra_square_for_last_place_helps_that_player()
    {
        var game = WithCompanyOf(4, players: 2);
        Hold(game, 1, 12);                               // P1 holds a stake in A...
        game.Players[1].Money = 0;                       // ...but is alone in last place
        game.Players[0].Money = 1_000_000;
        game.EndTurn();

        var hand = game.HandOf(1);
        Assert.Equal(ModernRules.HandSize + 1, hand.Count);
        var extra = hand[^1];
        var around = game.Board.Survey(extra);
        Assert.True(around.Companies.Contains(0) || (around.Companies.Count == 0 && around.NextToCircleOrDiamond),
            $"The extra square {extra} doesn't help: next to companies [{string.Join(",", around.Companies)}]");
    }

    /// <summary>A merger of B into A on P0's square, with P1 holding <paramref name="p1Shares"/> of B and P2 holding <paramref name="p2Shares"/>.</summary>
    private static (Game Game, MergerEvent Merger) MergeBIntoA(BigInteger p1Shares, BigInteger p2Shares)
    {
        var game = NewModern(players: 3, circles: [new Pos(2, 0), new Pos(6, 0)]);
        Play(game, new Pos(2, 1));        // P0 founds A
        game.EndTurn();
        Play(game, new Pos(6, 1));        // P1 founds B
        game.EndTurn();
        Play(game, new Pos(3, 1));        // P2 grows A
        game.EndTurn();
        Play(game, new Pos(5, 1));        // P0 grows B; A and B are one square apart
        game.EndTurn();
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        game.Players[1].Shares[1] = p1Shares;
        game.Players[2].Shares[1] = p2Shares;
        var merger = Assert.Single(Play(game, new Pos(4, 1)).OfType<MergerEvent>());
        return (game, merger);
    }

    [Fact]
    public void Merger_money_can_be_partly_converted_without_using_the_buy_allowance()
    {
        var (game, _) = MergeBIntoA(12, 0);
        var offer = Assert.Single(game.PendingConversions);
        Assert.Equal(1, offer.Player);
        Assert.Equal(0, offer.Survivor);
        long price = game.Companies[0].Value;
        var money = game.Players[1].Money;
        var allowance = game.BuyAllowanceLeft;

        var done = game.SettleConversions(new Dictionary<int, BigInteger> { [1] = 5 });
        Assert.Equal(5, Assert.Single(done!.Converted).Shares);
        Assert.Equal(5, game.Players[1].Shares[0]);
        Assert.Equal(money - 5 * price, game.Players[1].Money);
        Assert.Equal(allowance, game.BuyAllowanceLeft);
        Assert.Empty(game.PendingConversions);
    }

    [Fact]
    public void A_conversion_is_limited_to_the_money_owed()
    {
        var (game, _) = MergeBIntoA(12, 0);
        var offer = game.PendingConversions[0];
        long price = game.Companies[0].Value;
        game.SettleConversions(new Dictionary<int, BigInteger> { [1] = 1_000_000 });
        Assert.Equal(offer.Owed / price, game.Players[1].Shares[0]);
    }

    [Fact]
    public void When_shares_run_short_they_are_shared_out_by_money_owed()
    {
        var (game, _) = MergeBIntoA(30, 10);
        var offers = game.PendingConversions.ToDictionary(o => o.Player, o => o.Owed);
        Assert.Equal(2, offers.Count);
        // Leave only 20 survivor shares unheld (the survivor may already have split, doubling its shares).
        game.Players[0].Shares[0] = game.Companies[0].Supply - 20;
        game.Players[1].Money = game.Players[2].Money = 10_000_000;

        game.SettleConversions(new Dictionary<int, BigInteger> { [1] = 1_000, [2] = 1_000 });
        var p1 = game.Players[1].Shares[0];
        var p2 = game.Players[2].Shares[0];
        Assert.True(p1 + p2 <= 20);
        Assert.Equal(20 * offers[1] / (offers[1] + offers[2]), p1);
        Assert.Equal(20 * offers[2] / (offers[1] + offers[2]), p2);
    }

    [Fact]
    public void Unanswered_offers_stay_as_cash_and_none_are_made_at_the_end()
    {
        var (game, _) = MergeBIntoA(12, 0);
        var money = game.Players[1].Money;
        game.EndTurn();                                  // P1's turn starts, so P1 is also paid interest
        Assert.Empty(game.PendingConversions);
        Assert.Equal(money + game.InterestThisTurn, game.Players[1].Money);
        Assert.Equal(0, game.Players[1].Shares[0]);

        game.EndNow();
        Assert.Empty(game.PendingConversions);
    }

    [Fact]
    public void Each_affected_player_gets_a_merger_summary_in_their_own_terms()
    {
        var (game, merger) = MergeBIntoA(12, 0);
        var events = new List<GameEvent> { merger };
        string? p1 = EventText.MergerSummaryFor(game, merger, events, 1, Currency.Default);
        Assert.NotNull(p1);
        Assert.Contains("Betelguese was taken over by Atlantis Ltd.", p1);
        Assert.Contains("You were paid", p1);
        Assert.Contains("bonus", p1);
        Assert.Null(EventText.MergerSummaryFor(game, merger, events, 2, Currency.Default));   // P2 held neither company
    }

    [Fact]
    public void No_conversion_is_offered_when_the_money_cannot_buy_a_survivor_share()
    {
        var (game, merger) = MergeBIntoA(1, 0);          // P1 is paid for one Betelguese share, less than an Atlantis share
        var row = merger.Rows.Single(r => r.Player == 1);
        Assert.True(row.Total > 0 && row.Total < game.Companies[0].Value);
        Assert.Empty(game.PendingConversions);
    }
}
