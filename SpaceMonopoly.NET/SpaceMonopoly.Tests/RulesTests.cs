using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

public class RulesTests
{
    private static readonly GridSpec Small = GridSpec.For(GridSize.Small);

    /// <summary>Builds a game on an empty small board with circles at the given squares.</summary>
    internal static Game NewGame(int players = 2, GameType type = GameType.Classic, GameRules? rules = null, params Pos[] circles)
    {
        var board = new Board(Small.Width, Small.Height);
        foreach (var c in circles) board[c] = Board.Circle;
        var names = Enumerable.Range(0, players).Select(i => ($"P{i}", PlayerKind.Human));
        return new Game(Small, board, names, type, rules ?? new GameRules(), seed: 1);
    }

    private static IReadOnlyList<GameEvent> Play(Game game, Pos p)
    {
        game.SetChoicesForTesting(p);
        return game.PlaceSquare(p);
    }

    [Fact]
    public void Square_with_nothing_around_places_a_star()
    {
        var game = NewGame();
        var events = Play(game, new Pos(5, 5));

        Assert.Equal(Board.Diamond, game.Board[5, 5]);
        Assert.IsType<DiamondPlacedEvent>(Assert.Single(events));
    }

    [Fact]
    public void Square_next_to_circle_founds_company_and_rewards_founder()
    {
        var game = NewGame(circles: new Pos(5, 4));
        var events = Play(game, new Pos(5, 5));

        var founded = Assert.IsType<CompanyFoundedEvent>(Assert.Single(events));
        Assert.Equal(0, founded.Company);
        Assert.Equal(600, game.Companies[0].Value); // 100 + 500 for the circle
        Assert.Equal(5, game.Players[0].Shares[0]);
        Assert.Equal(Game.StartingMoney + 600, game.Players[0].Money);
    }

    [Fact]
    public void Founding_next_to_a_star_absorbs_it()
    {
        var game = NewGame();
        Play(game, new Pos(5, 5)); // star
        game.EndTurn();
        Play(game, new Pos(6, 5)); // next to the star → new company

        Assert.Equal(200, game.Companies[0].Value); // 100 + 100 for the star
        Assert.Equal(Board.CellForCompany(0), game.Board[5, 5]);
        Assert.Equal(2, game.Board.CompanySize(0));
    }

    [Fact]
    public void Expansion_raises_price_and_pays_every_shareholder_in_classic()
    {
        var game = NewGame(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5)); // P0 founds A at 600 with 5 shares
        game.Buy(0, 1);            // P0 now holds 6
        game.EndTurn();

        var events = Play(game, new Pos(6, 5)); // P1 grows A by 100

        var grew = Assert.IsType<CompanyExpandedEvent>(Assert.Single(events));
        Assert.Equal(100, grew.ValueAdded);
        Assert.Equal(700, game.Companies[0].Value);
        Assert.Equal(Game.StartingMoney + 600 - 600 + 600, game.Players[0].Money); // +6 x 100 dividend
        Assert.Equal(Game.StartingMoney, game.Players[1].Money);
    }

    [Fact]
    public void Merger_pays_out_absorbed_holders_and_converts_their_shares()
    {
        var game = NewGame(circles: [new Pos(2, 2), new Pos(6, 2)]);
        Play(game, new Pos(2, 3)); game.EndTurn(); // P0 founds A (600)
        Play(game, new Pos(6, 3)); game.EndTurn(); // P1 founds B (600)
        Play(game, new Pos(3, 3)); game.EndTurn(); // P0 grows A to 700 (+500 dividend)
        Play(game, new Pos(5, 3)); game.EndTurn(); // P1 grows B to 700 (+500 dividend)

        var events = Play(game, new Pos(4, 3));    // P0 joins them: equal size, so A survives

        var merger = Assert.IsType<MergerEvent>(events[0]);
        Assert.Equal(0, merger.Survivor);
        Assert.Equal(1, merger.Absorbed);
        Assert.IsType<CompanyExpandedEvent>(events[1]);

        Assert.False(game.Companies[1].Exists);
        Assert.Equal(1400 + 100, game.Companies[0].Value);
        Assert.Equal(5, game.Board.CompanySize(0));
        Assert.Equal(0, game.Board.CompanySize(1));

        Assert.Equal(5, game.Players[1].Shares[0]); // B shares became A shares
        Assert.Equal(0, game.Players[1].Shares[1]);
        Assert.Equal(6000 + 600 + 500 + 5 * 700 + 5 * 100, game.Players[1].Money);
        Assert.Equal(6000 + 600 + 500 + 5 * 100, game.Players[0].Money);
    }

    [Fact]
    public void Bigger_company_survives_a_merger()
    {
        var game = NewGame(circles: [new Pos(2, 2), new Pos(6, 2)]);
        Play(game, new Pos(2, 3)); game.EndTurn(); // A
        Play(game, new Pos(6, 3)); game.EndTurn(); // B
        Play(game, new Pos(7, 3)); game.EndTurn(); // B grows
        Play(game, new Pos(8, 3)); game.EndTurn(); // B grows again (size 3)
        Play(game, new Pos(3, 3)); game.EndTurn(); // A grows (size 2)
        Play(game, new Pos(5, 3)); game.EndTurn(); // B grows (size 4)

        var merger = Assert.IsType<MergerEvent>(Play(game, new Pos(4, 3))[0]);
        Assert.Equal(1, merger.Survivor);
        Assert.False(game.Companies[0].Exists);
    }

    [Fact]
    public void Price_of_3200_or_more_splits_two_for_one()
    {
        var game = NewGame(circles: [new Pos(5, 4), new Pos(4, 5), new Pos(6, 5), new Pos(4, 6), new Pos(6, 6), new Pos(5, 7)]);
        Play(game, new Pos(5, 5)); // 100 + 3 circles = 1,600
        Assert.Equal(1600, game.Companies[0].Value);
        game.EndTurn();

        var events = Play(game, new Pos(5, 6)); // +1,600 → 3,200 → split

        var split = Assert.IsType<SplitEvent>(events[^1]);
        Assert.Equal(3200, split.OldValue);
        Assert.Equal(1600, game.Companies[0].Value);
        Assert.Equal(10, game.Players[0].Shares[0]);
    }

    [Fact]
    public void Forced_merger_rejects_other_squares()
    {
        var game = NewGame(rules: new GameRules { ForceMergers = true }, circles: [new Pos(2, 2), new Pos(6, 2)]);
        Play(game, new Pos(2, 3)); game.EndTurn();
        Play(game, new Pos(6, 3)); game.EndTurn();
        Play(game, new Pos(3, 3)); game.EndTurn();
        Play(game, new Pos(5, 3)); game.EndTurn();

        game.SetChoicesForTesting(new Pos(4, 3), new Pos(12, 9));

        Assert.Equal("You must choose a square that causes a merger.", game.ValidateChoice(new Pos(12, 9)));
        Assert.Null(game.ValidateChoice(new Pos(4, 3)));
        Assert.Equal([new Pos(4, 3)], game.LegalChoices());
    }

    [Fact]
    public void Forced_expansion_can_allow_founding_instead()
    {
        var game = NewGame(rules: new GameRules { ForceExpansion = true, AllowCompanyCreation = true },
            circles: [new Pos(2, 2), new Pos(10, 2)]);
        Play(game, new Pos(2, 3)); game.EndTurn();

        game.SetChoicesForTesting(new Pos(3, 3), new Pos(10, 3), new Pos(14, 10));

        Assert.Null(game.ValidateChoice(new Pos(3, 3)));   // expands
        Assert.Null(game.ValidateChoice(new Pos(10, 3)));  // founds
        Assert.NotNull(game.ValidateChoice(new Pos(14, 10))); // just a star
    }

    [Fact]
    public void Tournament_players_have_separate_boards()
    {
        var game = NewGame(type: GameType.Tournament, circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));
        game.EndTurn();

        Assert.Equal(GameType.Tournament, game.Type);
        Assert.Equal(Board.Empty, game.Board[5, 5]);
        Assert.False(game.Companies[0].Exists);
        Assert.True(game.WorldFor(0).Companies[0].Exists);
    }

    [Fact]
    public void Single_player_tournament_becomes_classic()
    {
        Assert.Equal(GameType.Classic, NewGame(players: 1, type: GameType.Tournament).Type);
    }

    [Fact]
    public void Trading_is_only_allowed_after_choosing_a_square()
    {
        var game = NewGame(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));
        game.EndTurn();

        Assert.Equal("You can only trade after choosing a square.", game.ValidateTrade(0, 1, isBuy: true));
        Assert.Throws<InvalidOperationException>(() => game.Buy(0, 1));
    }

    [Fact]
    public void Cannot_buy_more_than_you_can_afford_or_sell_more_than_you_own()
    {
        var game = NewGame(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));

        Assert.Equal(11, game.MaxBuy(0)); // 6,600 / 600
        Assert.NotNull(game.ValidateTrade(0, 12, isBuy: true));
        Assert.NotNull(game.ValidateTrade(0, 6, isBuy: false));
        game.Sell(0, 5);
        Assert.Equal(Game.StartingMoney + 600 + 3000, game.Players[0].Money);
    }

    [Theory]
    [InlineData(GameType.Classic, 3, 80)]
    [InlineData(GameType.Tournament, 3, 80 * 3)]
    public void Game_ends_after_the_right_number_of_goes(GameType type, int players, int expectedMoves)
    {
        var board = new Board(Small.Width, Small.Height);
        var game = new Game(Small, board, Enumerable.Range(0, players).Select(i => ($"P{i}", PlayerKind.Human)), type, new GameRules(), seed: 3);

        int moves = 0;
        while (game.Phase != GamePhase.Over)
        {
            game.PlaceSquare(game.LegalChoices()[0]);
            moves++;
            game.EndTurn();
        }
        Assert.Equal(expectedMoves, moves);
    }

    [Fact]
    public void Net_worth_counts_cash_and_shares()
    {
        var game = NewGame(circles: new Pos(5, 4));
        Play(game, new Pos(5, 5));
        Assert.Equal(6600 + 5 * 600, game.NetWorth(0));
        Assert.Equal("P0", game.Standings()[0].Name);
    }
}
