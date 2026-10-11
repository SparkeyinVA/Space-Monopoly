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
        NextEvent = DrawCard();
        StartRound();
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

    /// <summary>Flips the next card and applies it.</summary>
    private void StartRound()
    {
        CurrentEvent = NextEvent;
        NextEvent = DrawCard();
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
                var target = live.Where(c => _players[leader].Shares[c.Index] > 0)
                    .OrderByDescending(c => _players[leader].Shares[c.Index] * c.Value).FirstOrDefault();
                if (target is null) break;
                long old = Lower(target, target.Value * EventCards.ScandalPercent / 100);
                return new(card, round, target.Index, old, target.Value, Payments: [new Payout(leader, 0)]);
            }

            case EventCard.GalacticGrant:
            {
                int poorest = Enumerable.Range(0, _players.Length).OrderBy(NetWorth).First();
                _players[poorest].Money += EventCards.Grant;
                return new(card, round, Payments: [new Payout(poorest, EventCards.Grant)]);
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
