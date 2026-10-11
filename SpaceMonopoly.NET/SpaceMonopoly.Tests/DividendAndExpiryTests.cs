using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

/// <summary>Dividend Day (S17) and squares expiring from a hand (S18).</summary>
public class DividendAndExpiryTests
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
            game.PlaceSquare(game.Choices[^1]);
            game.EndTurn();
        } while (game.CurrentPlayerIndex != 0);
    }

    [Fact]
    public void A_square_expires_after_four_turns_in_hand()
    {
        var game = Modern();
        var kept = game.HandOf(0)[0];
        for (int turn = 0; turn < ModernRules.SquareExpiryTurns; turn++)
        {
            Assert.Contains(kept, game.HandOf(0));
            Assert.Equal(ModernRules.SquareExpiryTurns - turn, game.TurnsLeft(0, kept));
            PlayRoundKeepingFirst(game);
        }
        Assert.DoesNotContain(kept, game.HandOf(0));
        Assert.Equal(ModernRules.HandSize, game.HandOf(0).Count);
    }

    [Fact]
    public void A_square_drawn_after_playing_gets_four_full_turns()
    {
        var game = Modern();
        var before = game.HandOf(0).ToList();
        game.PlaceSquare(game.Choices[^1]);
        var drawn = game.HandOf(0).Except(before).Single();          // the replacement for the square just played
        Assert.Equal(ModernRules.SquareExpiryTurns + 1, game.TurnsLeft(0, drawn));   // counts from P0's next turn
        game.EndTurn();
        game.PlaceSquare(game.Choices[0]);
        game.EndTurn();
        Assert.Equal(ModernRules.SquareExpiryTurns, game.TurnsLeft(0, drawn));
    }

    [Fact]
    public void Swapped_and_new_hand_squares_start_counting_again()
    {
        var game = Modern();
        PlayRoundKeepingFirst(game);
        var before = game.HandOf(0).ToList();
        game.Swap(before[0]);                            // P0's oldest square, with 3 turns left
        var swappedIn = game.HandOf(0).Except(before).Single();
        Assert.Equal(ModernRules.SquareExpiryTurns, game.TurnsLeft(0, swappedIn));
        game.Prospect();
        Assert.All(game.HandOf(0), p => Assert.Equal(ModernRules.SquareExpiryTurns, game.TurnsLeft(0, p)));
    }

    [Fact]
    public void Last_place_still_gets_its_extra_square_after_an_expiry()
    {
        var game = Modern();
        for (int r = 0; r < ModernRules.SquareExpiryTurns - 1; r++) PlayRoundKeepingFirst(game);
        game.Players[0].Money = 0;
        game.Players[1].Money = 1_000_000;
        PlayRoundKeepingFirst(game);                     // P0's first square expires at the end of this turn
        Assert.Equal(ModernRules.HandSize + 1, game.HandOf(0).Count);
    }
}
