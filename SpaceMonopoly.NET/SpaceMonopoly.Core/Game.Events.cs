namespace SpaceMonopoly.Core;

/// <summary>Modern only: the events deck. Each round flips one card; the next round's card is always known.</summary>
public sealed partial class Game
{
    private List<EventCard> _deck = [];
    private int _deckIndex;

    // When each company square was claimed, so an Asteroid Strike can remove the newest one.
    private Dictionary<Pos, int> _claimedAt = [];
    private int _claims;

    /// <summary>The card in effect this round (Modern only).</summary>
    public EventCard? CurrentEvent { get; private set; }

    /// <summary>The card that will be flipped at the start of next round, shown in advance (Modern only).</summary>
    public EventCard? NextEvent { get; private set; }

    /// <summary>What the event card did at the start of the current round. Empty at other times.</summary>
    public IReadOnlyList<GameEvent> RoundStartEvents { get; private set; } = [];

    private bool SolarFlare => CurrentEvent == EventCard.SolarFlare;
    private bool MergerMania => CurrentEvent == EventCard.MergerMania;

    private void InitEvents()
    {
        _deck = EventCards.Deck(Rules.Chaos);
        Shuffle(_deck);
        // No events until round 3 (F6); round 3's card is announced from the start.
        NextEvent = DrawUsefulCard();
        CurrentEvent = null;
    }

    /// <summary>Lets tests choose the cards: the deck is replaced and the next card announced from it.</summary>
    internal void SetDeckForTesting(params EventCard[] cards)
    {
        _deck = [.. cards];
        _deckIndex = 0;
        NextEvent = DrawUsefulCard();
    }

    /// <summary>The round whose card <see cref="NextEvent"/> is.</summary>
    public int NextEventRound => Math.Max(Turn + 2, EventCards.FirstEventRound);

    /// <summary>
    /// Draws the next card to announce, setting aside any that couldn't affect anything right now (F6), so the
    /// announced card is the one that's played. Quiet Quarter is never set aside.
    /// </summary>
    private EventCard DrawUsefulCard()
    {
        for (int tries = 0; tries < _deck.Count; tries++)
        {
            var card = DrawCard();
            if (CouldAffect(card)) return card;
        }
        return EventCard.QuietQuarter;
    }

    /// <summary>True if the card would do something if it were played now.</summary>
    private bool CouldAffect(EventCard card)
    {
        var live = Companies.Where(c => c.Exists).ToList();
        IEnumerable<Surroundings> Empties() =>
            Board.AllPositions().Where(p => Board[p] == Board.Empty).Select(Board.Survey);
        return card switch
        {
            EventCard.QuietQuarter => true,
            EventCard.SolarFlare => Empties().Any(s => s.Companies.Count > 0),
            EventCard.MergerMania => Empties().Any(s => s.Companies.Count > 1),
            EventCard.PirateRaid or EventCard.MarketBoom => live.Count > 0,
            EventCard.AsteroidStrike => live.Any(c => Board.CompanySize(c.Index) > 1),
            EventCard.InsiderScandal => ScandalTarget() is not null,
            EventCard.GalacticGrant => GrantRecipient() is not null,
            EventCard.PlanetDiscovered => Empties().Any(s => s.Companies.Count == 0),
            EventCard.TaxAudit => true,
            EventCard.DividendDay => _players.Any(p => live.Any(c => p.Shares[c.Index] > 0)),
            _ => true,
        };
    }

    /// <summary>The player who would receive a Galactic Grant now and how much, or null if no one is far enough behind (F5).</summary>
    public (int Player, BigInteger Amount)? GrantRecipient()
    {
        var worth = Enumerable.Range(0, _players.Length).Select(NetWorth).ToArray();
        int poorest = Array.IndexOf(worth, worth.Min());
        if (_players.Length < 2 || worth[poorest] * 100 >= worth.Max() * EventCards.GrantBelowLeaderPercent) return null;
        var average = worth.Aggregate(BigInteger.Zero, (a, b) => a + b) / worth.Length;
        var amount = average * EventCards.GrantPercent / 100 / 100 * 100;   // rounded down to 100
        return amount > 0 ? (poorest, amount) : null;
    }

    private Company? ScandalTarget()
    {
        int leader = Enumerable.Range(0, _players.Length).OrderByDescending(NetWorth).First();
        return Companies.Where(c => c.Exists && _players[leader].Shares[c.Index] > 0)
            .OrderByDescending(c => _players[leader].Shares[c.Index] * c.Value).FirstOrDefault();
    }

    /// <summary>The card's effect in words, with the amounts it would use now (for the side panel and tooltips).</summary>
    public string DescribeEvent(EventCard card)
    {
        if (card == EventCard.GalacticGrant)
            return GrantRecipient() is var (player, amount)
                ? $"The poorest player receives {EventCards.GrantPercent}% of the average net worth (about {Currency.Default.Format(amount)} now; {_players[player].Name} at the moment)."
                : $"The poorest player receives {EventCards.GrantPercent}% of the average net worth, but only if they're below {EventCards.GrantBelowLeaderPercent}% of the leader (no one is now).";
        return EventCards.Description(card);
    }

    private void CopyEvents(Game other)
    {
        _deck = [.. other._deck];
        _deckIndex = other._deckIndex;
        _claimedAt = new Dictionary<Pos, int>(other._claimedAt);
        _claims = other._claims;
        CurrentEvent = other.CurrentEvent;
        NextEvent = other.NextEvent;
        RoundStartEvents = other.RoundStartEvents;
    }

    private void Shuffle(List<EventCard> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = _rng.Next(i + 1);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }

    private EventCard DrawCard()
    {
        if (_deckIndex >= _deck.Count)
        {
            Shuffle(_deck);
            _deckIndex = 0;
        }
        return _deck[_deckIndex++];
    }

    /// <summary>Flips the announced card and applies it (from round 3), and announces the next one.</summary>
    private void StartRound()
    {
        if (Turn + 1 < EventCards.FirstEventRound)
        {
            CurrentEvent = null;
            RoundStartEvents = [];
            return;
        }
        CurrentEvent = NextEvent;
        NextEvent = DrawUsefulCard();
        var splits = new List<GameEvent>();
        RoundStartEvents = [ApplyEvent(CurrentEvent!.Value, splits), .. splits];
    }

    private void RecordClaim(Pos p) => _claimedAt[p] = ++_claims;

    private long Lower(Company c, long by)
    {
        long old = c.Value;
        c.Value = Math.Max(EventCards.MinimumPrice, c.Value - by);
        return old;
    }

    private EventCardEvent ApplyEvent(EventCard card, List<GameEvent> splits)
    {
        int round = Turn + 1;
        var live = Companies.Where(c => c.Exists).ToList();
        switch (card)
        {
            case EventCard.PirateRaid when live.Count > 0:
            {
                var target = live.OrderByDescending(c => c.Value).ThenBy(c => c.Index).First();
                long old = Lower(target, target.Value * EventCards.PirateRaidPercent / 100);
                return new(card, round, target.Index, old, target.Value);
            }

            case EventCard.AsteroidStrike:
            {
                var candidates = live.Where(c => Board.CompanySize(c.Index) > 1).ToList();
                if (candidates.Count == 0) break;
                var target = candidates[_rng.Next(candidates.Count)];
                int cell = Board.CellForCompany(target.Index);
                var newest = Board.AllPositions().Where(p => Board[p] == cell)
                    .OrderByDescending(p => _claimedAt.GetValueOrDefault(p)).First();
                Board[newest] = Board.Empty;
                long old = Lower(target, EventCards.AsteroidLoss);
                return new(card, round, target.Index, old, target.Value, newest);
            }

            case EventCard.MarketBoom when live.Count > 0:
                foreach (var c in live) c.Value += c.Value * EventCards.MarketBoomPercent / 100;
                foreach (var c in live) CheckSplit(c.Index, splits);
                return new(card, round);

            case EventCard.PlanetDiscovered:
            {
                var held = _hands.SelectMany(h => h).ToHashSet();
                var spots = Board.AllPositions()
                    .Where(p => Board[p] == Board.Empty && !held.Contains(p) && Board.Survey(p).Companies.Count == 0)
                    .ToList();
                if (spots.Count == 0) break;
                var spot = spots[_rng.Next(spots.Count)];
                Board[spot] = Board.Circle;
                return new(card, round, Square: spot);
            }

            case EventCard.InsiderScandal:
            {
                int leader = Enumerable.Range(0, _players.Length).OrderByDescending(NetWorth).First();
                var target = ScandalTarget();
                if (target is null) break;
                long old = Lower(target, target.Value * EventCards.ScandalPercent / 100);
                return new(card, round, target.Index, old, target.Value, Payments: [new Payout(leader, 0)]);
            }

            case EventCard.GalacticGrant:
            {
                if (GrantRecipient() is not var (poorest, amount)) break;   // no one far enough behind: nothing happens
                _players[poorest].Money += amount;
                return new(card, round, Payments: [new Payout(poorest, amount)]);
            }

            case EventCard.DividendDay:
            {
                // S17: per company held, not per share, so players who are behind gain the most and the leader can't scale it up.
                var paid = new List<Payout>();
                for (int i = 0; i < _players.Length; i++)
                {
                    int held = live.Count(c => _players[i].Shares[c.Index] > 0);
                    if (held == 0) continue;
                    BigInteger amount = EventCards.DividendPerCompany * held;
                    _players[i].Money += amount;
                    paid.Add(new Payout(i, amount));
                }
                return new(card, round, Payments: paid);
            }

            case EventCard.TaxAudit:
            {
                var worth = Enumerable.Range(0, _players.Length).Select(NetWorth).ToArray();
                var average = worth.Aggregate(BigInteger.Zero, (a, b) => a + b) / worth.Length;
                var taxed = new List<Payout>();
                for (int i = 0; i < _players.Length; i++)
                {
                    if (worth[i] <= average) continue;
                    var tax = _players[i].Money * EventCards.TaxPercent / 100;
                    _players[i].Money -= tax;
                    taxed.Add(new Payout(i, -tax));
                }
                return new(card, round, Payments: taxed);
            }
        }
        // Quiet Quarter, Solar Flare and Merger Mania (which last all round), or a card with nothing to hit.
        return new(card, round);
    }
}
