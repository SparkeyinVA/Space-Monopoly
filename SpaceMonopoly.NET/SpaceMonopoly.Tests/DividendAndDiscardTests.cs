using SpaceMonopoly.Core;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Tests;

/// <summary>Dividend Day (S17), and discarding and the hand limit in place of squares expiring (S20).</summary>
public class DividendAndDiscardTests
{
    private static readonly GridSpec Medium = GridSpec.For(GridSize.Medium);

    private static Game Modern(int players = 2, Chaos chaos = Chaos.None, params Pos[] circles)
    {
        var board = new Board(Medium.Width, Medium.Height);
        foreach (var c in circles) board[c] = Board.Circle;
        return new Game(Medium, board, Enumerable.Range(0, players).Select(i => ($"P{i}", PlayerKind.Human)), GameType.Modern,
            new GameRules { Chaos = chaos }, seed: 4);
    }

    [Fact]
    public void Dividend_Day_pays_per_company_held_not_per_share()
    {
        var game = Modern(players: 2, Chaos.Standard, circles: [new Pos(2, 0), new Pos(8, 0)]);
        game.SetChoicesForTesting(new Pos(2, 1));
        game.PlaceSquare(new Pos(2, 1));                 // P0 founds A (3 shares)
        game.EndTurn();
        game.SetChoicesForTesting(new Pos(8, 1));
        game.PlaceSquare(new Pos(8, 1));                 // P1 founds B (3 shares)
        game.Players[1].Shares[0] = 1;                   // and holds a single share of A
        game.SetDeckForTesting(EventCard.DividendDay);   // announced now that someone holds shares
        game.EndTurn();
        // Rounds 1 and 2 have no events; Dividend Day comes in round 3.
        while (game.CurrentEvent is null)
        {
            game.PlaceSquare(game.Choices[0]);
            game.EndTurn();
        }
        var card = Assert.Single(game.RoundStartEvents.OfType<EventCardEvent>());
        Assert.Equal(EventCard.DividendDay, card.Card);
        var paid = card.Payments!.ToDictionary(p => p.Player, p => p.Amount);
        // Each player is paid per company they hold any shares in, however many or few.
        for (int p = 0; p < 2; p++)
        {
            int held = game.Companies.Count(c => c.Exists && game.Players[p].Shares[c.Index] > 0);
            Assert.Equal(new BigInteger(held * EventCards.DividendPerCompany), paid.GetValueOrDefault(p));
        }
        Assert.True(game.Players[1].Shares[0] <= 1);    // P1's single share of A still counted in full
    }

    [Fact]
    public void Standard_has_two_Dividend_Days_in_place_of_two_Quiet_Quarters()
    {
        var deck = EventCards.Deck(Chaos.Standard);
        Assert.Equal(20, deck.Count);
        Assert.Equal(2, deck.Count(c => c == EventCard.DividendDay));
        Assert.Equal(4, deck.Count(c => c == EventCard.QuietQuarter));
    }

    /// <summary>Plays P0's last hand square each turn, so P0's first square is never played.</summary>
    private static void PlayRoundKeepingFirst(Game game)
    {
        do
        {
            while (game.MustDrop) game.Drop(game.Choices[^1]);   // P0's first square stays
            game.PlaceSquare(game.Choices[^1]);
            game.EndTurn();
        } while (game.CurrentPlayerIndex != 0);
    }

    [Fact]
    public void Squares_never_expire()
    {
        var game = Modern();
        var kept = game.HandOf(0)[0];
        for (int round = 0; round < 8; round++)
        {
            Assert.Contains(kept, game.HandOf(0));
            PlayRoundKeepingFirst(game);
        }
        Assert.Contains(kept, game.HandOf(0));
    }

    [Fact]
    public void A_discard_is_replaced_at_the_start_of_the_next_turn_once_per_turn()
    {
        var game = Modern();
        Assert.False(game.CanDiscard);                   // not before playing a square
        game.PlaceSquare(game.Choices[^1]);
        Assert.True(game.CanDiscard);
        var discarded = game.HandOf(0)[0];
        game.Discard(discarded);
        Assert.Equal(ModernRules.HandSize - 1, game.HandOf(0).Count);   // one short during the others' turns
        Assert.False(game.CanDiscard);
        Assert.Throws<InvalidOperationException>(() => game.Discard(game.HandOf(0)[0]));

        game.EndTurn();
        Assert.Equal(ModernRules.HandSize - 1, game.HandOf(0).Count);
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);       // drawn at the start of P0's turn
        Assert.DoesNotContain(discarded, game.HandOf(0));
    }

    [Fact]
    public void The_replacement_is_never_the_discarded_square_even_when_little_is_left()
    {
        // Ten empty squares among stars: both hands of 4 and two to spare, so the discarded square is nearly all that's left.
        for (int seed = 1; seed <= 20; seed++)
        {
            var board = new Board(Medium.Width, Medium.Height);
            foreach (var p in board.AllPositions()) board[p] = Board.Diamond;
            for (int x = 0; x < 10; x++) board[new Pos(2 * x, 6)] = Board.Empty;
            var game = new Game(Medium, board, [("P0", PlayerKind.Human), ("P1", PlayerKind.Human)], GameType.Modern,
                new GameRules { Chaos = Chaos.None }, seed);
            game.PlaceSquare(game.Choices[0]);
            var discarded = game.HandOf(0)[0];
            game.Discard(discarded);
            game.EndTurn();
            if (game.Phase == GamePhase.Over) continue;
            game.PlaceSquare(game.Choices[0]);
            if (game.EndTurn()) continue;
            Assert.DoesNotContain(discarded, game.HandOf(0));
            Assert.Equal(game.HandOf(0).Count < game.HandLimit(0), game.HandShort);   // short only when nothing else was left
        }
    }

    [Fact]
    public void Becoming_last_fills_to_five_and_stopping_being_last_means_choosing_a_square_to_drop()
    {
        var game = Modern();
        game.Players[0].Money = 0;
        game.Players[1].Money = 1_000_000;
        PlayRoundKeepingFirst(game);
        Assert.Equal(ModernRules.HandSize + 1, game.HandOf(0).Count);   // alone in last place
        Assert.False(game.MustDrop);

        game.PlaceSquare(game.Choices[^1]);
        game.Players[0].Money = 2_000_000;               // P0 is no longer last
        game.EndTurn();
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();

        Assert.Equal(ModernRules.HandSize + 1, game.HandOf(0).Count);
        Assert.True(game.MustDrop);
        Assert.False(game.CanSwap);
        Assert.Throws<InvalidOperationException>(() => game.PlaceSquare(game.Choices[0]));
        var dropped = game.HandOf(0)[2];                 // the player chooses which
        game.Drop(dropped);
        Assert.DoesNotContain(dropped, game.HandOf(0));
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);
        Assert.False(game.MustDrop);
        game.PlaceSquare(game.Choices[0]);
    }

    [Fact]
    public void Computer_players_start_every_turn_with_exactly_the_hand_allowed()
    {
        var game = new Game(Medium, MakeBoard(), [("A", PlayerKind.ComputerHard), ("B", PlayerKind.ComputerMedium), ("C", PlayerKind.ComputerEasy)],
            GameType.Modern, new GameRules { Chaos = Chaos.Standard }, seed: 7);
        var ais = new[] { new RuleBasedAi(AiLevel.Hard, 1), new RuleBasedAi(AiLevel.Medium, 2), new RuleBasedAi(AiLevel.Easy, 3) };
        int discards = 0;
        while (game.Phase != GamePhase.Over)
        {
            int me = game.CurrentPlayerIndex;
            ais[me].DropForHandLimit(game);
            if (!game.HandShort) Assert.Equal(game.HandLimit(me), game.HandOf(me).Count);
            game.PlaceSquare(ais[me].ChooseSquare(game));
            if (game.PendingConversions.Count > 0) game.SettleConversions(game.PendingConversions.ToDictionary(o => o.Player, _ => BigInteger.Zero));
            TradeExecutor.Apply(game, ais[me].PlanTrades(game));
            if (ais[me].ChooseDiscard(game) is Pos d) { game.Discard(d); discards++; }
            game.EndTurn();
        }
        Assert.True(discards > 0);
    }

    private static Board MakeBoard()
    {
        var board = new Board(Medium.Width, Medium.Height);
        board.Randomise(new Random(7), ModernRules.PlanetOdds);
        return board;
    }
}
