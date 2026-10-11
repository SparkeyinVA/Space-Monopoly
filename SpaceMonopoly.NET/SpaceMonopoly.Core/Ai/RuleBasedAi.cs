namespace SpaceMonopoly.Core.Ai;

public enum AiLevel { Easy, Medium, Normal, Hard }

/// <summary>
/// Built-in computer opponent. Strategy follows the hints in the original documentation:
/// buy cheap companies that can grow next to circles, build companies close together so they merge,
/// and move money out of companies that have stopped growing.
/// </summary>
/// <remarks>
/// Easy mostly plays at random. Medium judges each square by its immediate result and never sells.
/// Normal looks ahead like Hard but plays its second-best square some of the time, and trades like Medium.
/// Hard simulates every square plus its follow-up trades and compares itself against the leading opponent.
/// </remarks>
public sealed class RuleBasedAi : IAiPlayer
{
    private readonly Random _rng;

    public RuleBasedAi(AiLevel level, int? seed = null, Personality personality = Personality.None)
    {
        Level = level;
        Personality = personality;
        _profile = Personalities.Profile(personality);
        _rng = seed is int s ? new Random(s) : new Random();
    }

    private readonly PersonalityProfile _profile;

    public AiLevel Level { get; }

    /// <summary>How this player likes to play (S9). It changes the weighting of moves, not the skill level.</summary>
    public Personality Personality { get; }

    /// <summary>Normal: how often it plays its second-best square instead of its best (S9). Adaptive difficulty moves it.</summary>
    public double SecondBestChance { get; set; } = 0.1;

    public string? LastComment => null;

    public Task<Pos> ChooseSquareAsync(Game game, CancellationToken cancellationToken) =>
        Task.FromResult(ChooseSquare(game));

    public Task<IReadOnlyList<TradeOrder>> PlanTradesAsync(Game game, CancellationToken cancellationToken) =>
        Task.FromResult(PlanTrades(game));

    /// <summary>
    /// Picks a square to play. In a Modern game, Medium and Hard first use their one prospect
    /// (a new hand) if every square they hold would only place a star.
    /// </summary>
    public Pos ChooseSquare(Game game)
    {
        if (game.CanProspect && Level != AiLevel.Easy && MovePreview.ForChoices(game).All(m => m.Kind == MoveKind.Star))
            game.Prospect();

        // Swap the square that does least for us, if it does nothing at all.
        if (game.CanSwap && Level != AiLevel.Easy)
        {
            int me0 = game.CurrentPlayerIndex;
            var worst = MovePreview.ForChoices(game).OrderBy(m => m.NetWorthChange[me0]).ThenBy(m => m.Kind == MoveKind.Star ? 0 : 1).First();
            if (worst.NetWorthChange[me0] <= 0) game.Swap(worst.At);
        }

        var legal = game.LegalChoices();
        if (legal.Count == 0) throw new InvalidOperationException("No legal squares to choose from.");
        int me = game.CurrentPlayerIndex;

        if (Level == AiLevel.Easy && _rng.NextDouble() < 0.6)
            return legal[_rng.Next(legal.Count)];

        Pos best = legal[0], second = legal[0];
        double bestScore = double.NegativeInfinity, secondScore = double.NegativeInfinity;
        foreach (var square in legal)
        {
            var sim = game.Clone();
            sim.PlaceSquare(square);

            double score = Level switch
            {
                AiLevel.Easy => (double)sim.NetWorth(me),
                AiLevel.Medium => Heuristics.RelativeOutlook(sim, me, opponentWeight: 0.25),
                AiLevel.Normal => ScoreAfterTrades(sim, me, opponentWeight: 0.25),
                _ => ScoreAfterTrades(sim, me, opponentWeight: 0.5),
            };
            // A personality's taste for mergers or founding, scaled to the player's wealth.
            if (Level != AiLevel.Easy) score += PersonalityBias(game, square, me);

            // Small random tie-breaker so equal squares aren't always picked in the same order.
            score += _rng.NextDouble() * 0.01;

            if (score > bestScore)
            {
                (second, secondScore) = (best, bestScore);
                (best, bestScore) = (square, score);
            }
            else if (score > secondScore)
            {
                (second, secondScore) = (square, score);
            }
        }
        if (Level == AiLevel.Normal && legal.Count > 1 && _rng.NextDouble() < SecondBestChance) return second;
        return best;
    }

    private double PersonalityBias(Game game, Pos square, int me)
    {
        if (_profile.MergerBias == 0 && _profile.FoundBias == 0) return 0;
        var around = game.Board.Survey(square);
        double worth = (double)game.NetWorth(me);
        if (around.Companies.Count > 1) return _profile.MergerBias * worth;
        if (around.Companies.Count == 0 && around.NextToCircleOrDiamond && game.Companies.Any(c => !c.Exists)) return _profile.FoundBias * worth;
        return 0;
    }

    private double ScoreAfterTrades(Game sim, int me, double opponentWeight)
    {
        TradeExecutor.Apply(sim, PlanTrades(sim));
        return Heuristics.RelativeOutlook(sim, me, opponentWeight);
    }

    public IReadOnlyList<TradeOrder> PlanTrades(Game game)
    {
        var existing = game.Companies.Where(c => c.Exists).ToList();
        if (existing.Count == 0) return [];
        return Level switch
        {
            AiLevel.Easy => PlanEasyTrades(game, existing),
            AiLevel.Medium or AiLevel.Normal => PlanInvestment(game, sellThreshold: null, readEvents: false),
            _ => PlanInvestment(game, sellThreshold: HardSells(game) ? _profile.SellAt : null, readEvents: true),
        };
    }

    private List<TradeOrder> PlanEasyTrades(Game game, List<Company> existing)
    {
        var orders = new List<TradeOrder>();
        var me = game.CurrentPlayer;

        var held = existing.Where(c => me.Shares[c.Index] > 0).ToList();
        if (held.Count > 0 && _rng.NextDouble() < 0.15)
            orders.Add(new TradeOrder(TradeAction.Sell, held[_rng.Next(held.Count)].Index, 0));

        var affordable = existing.Where(c => c.Value <= me.Money).ToList();
        if (affordable.Count > 0 && _rng.NextDouble() < 0.8)
        {
            var pick = affordable[_rng.Next(affordable.Count)];
            var max = game.MaxBuy(pick.Index);
            if (max > 0) orders.Add(new TradeOrder(TradeAction.Buy, pick.Index, BigInteger.Max(1, max / 2)));
        }
        return orders;
    }

    /// <summary>
    /// Whether Hard sells to move money into faster-growing companies. Measured with the balance tool: it helps in
    /// Modern and in two-player Classic games, but in Classic with three or more players it let Medium win most games
    /// (Hard 32%, Medium 49% against Hard 49%, Medium 27% without selling).
    /// </summary>
    private static bool HardSells(Game game) => game.IsModern || game.Players.Count <= 2;

    /// <summary>
    /// Sells holdings that are growing much slower than the best company (Hard only), then spends
    /// as much as possible on the best expected return, then the next best with what's left.
    /// </summary>
    private List<TradeOrder> PlanInvestment(Game game, double? sellThreshold, bool readEvents)
    {
        var orders = new List<TradeOrder>();
        var me = game.CurrentPlayer;
        var ratio = Heuristics.ReturnPerUnit(game, game.CurrentWorld);

        // Hard reads next round's announced event: it avoids a Pirate Raid's target and buys ahead of a Market Boom.
        if (readEvents)
        {
            var effect = Heuristics.NextEventEffect(game);
            for (int c = 0; c < ratio.Length; c++) ratio[c] += effect[c] < 0 ? effect[c] * _profile.EventCaution : effect[c];
        }
        // In Modern, cash earns interest, so only companies expected to beat it are worth buying.
        double interest = Heuristics.InterestReturn(game, game.CurrentPlayerIndex);
        var ranked = Enumerable.Range(0, game.Companies.Length)
            .Where(c => game.Companies[c].Exists && ratio[c] > interest)
            .OrderByDescending(c => ratio[c])
            .Take(_profile.GrowthCompanies)
            .ToList();
        if (ranked.Count == 0 && !game.IsModern) return orders;

        double bestRatio = ranked.Count > 0 ? ratio[ranked[0]] : 0;
        int top = ranked.Count > 0 ? ranked[0] : -1;
        var cash = me.Money;
        int self = game.CurrentPlayerIndex;
        var held = me.Shares.ToArray();   // what we'll hold once this turn's sales are done

        if (sellThreshold is double threshold)
        {
            for (int c = 0; c < game.Companies.Length; c++)
            {
                if (!game.Companies[c].Exists || me.Shares[c] == 0 || c == top) continue;
                // Modern: don't give up a stake that's on course for a majority bonus.
                if (Heuristics.BonusAt(game, self, c, me.Shares[c]) > 0) continue;
                if (ratio[c] < bestRatio * threshold)
                {
                    orders.Add(new TradeOrder(TradeAction.Sell, c, 0));
                    cash += me.Shares[c] * game.Companies[c].Value;
                    held[c] = 0;
                }
            }
        }

        // Modern caps how many shares can be bought in a turn and how many each company has left.
        var allowance = game.BuyAllowanceLeft;

        // A Hoarder keeps part of its net worth back as cash early on and buys in late (Modern, where cash earns interest).
        if (game.IsModern && _profile.CashKeep > 0)
        {
            double share = _profile.CashKeep * Math.Max(0, game.Rounds - game.Turn - 1) / Math.Max(1, game.Rounds);
            var keep = new BigInteger((double)game.NetWorth(self) * share);
            cash = BigInteger.Max(0, cash - keep);
        }

        // Modern: first buy into majorities (F4), where a stake earns a bonus worth more than buying for growth.
        if (game.IsModern) BuyStakes(game, ratio, orders, held, ref cash, ref allowance, _profile.BonusWeight);

        foreach (int c in ranked)
        {
            long price = game.Companies[c].Value;
            if (price > cash || allowance == 0) continue;
            var qty = cash / price;
            if (allowance > 0) qty = BigInteger.Min(qty, allowance);
            if (game.SharesLeft(c) is var left && left >= 0) qty = BigInteger.Min(qty, left);
            if (qty <= 0) continue;
            orders.Add(new TradeOrder(TradeAction.Buy, c, qty));
            cash -= qty * price;
            if (allowance > 0) allowance -= qty;
        }
        return orders;
    }

    /// <summary>
    /// Modern: buys the stakes that raise the player's majority bonuses the most for the money, as long as each
    /// beats the interest cash would earn. Tries reaching the 10-share minimum and overtaking or tying each rival.
    /// </summary>
    private static void BuyStakes(Game game, double[] ratio, List<TradeOrder> orders, BigInteger[] held,
        ref BigInteger cash, ref BigInteger allowance, double bonusWeight)
    {
        int me = game.CurrentPlayerIndex;
        double interest = Heuristics.InterestReturn(game, me);

        for (int round = 0; round < 4 && allowance != 0; round++)
        {
            (int Company, BigInteger Qty, double Score) best = (-1, 0, interest);
            for (int c = 0; c < game.Companies.Length; c++)
            {
                var company = game.Companies[c];
                if (!company.Exists || company.Value <= 0) continue;
                var cap = cash / company.Value;
                if (allowance > 0) cap = BigInteger.Min(cap, allowance);
                if (game.SharesLeft(c) is var left && left >= 0) cap = BigInteger.Min(cap, left - (held[c] - game.CurrentPlayer.Shares[c]));
                if (cap <= 0) continue;

                double now = Heuristics.BonusAt(game, me, c, held[c]);
                var targets = new List<BigInteger> { ModernRules.MinSharesForBonus - held[c] };
                for (int i = 0; i < game.Players.Count; i++)
                    if (i != me) targets.AddRange([game.Players[i].Shares[c] - held[c], game.Players[i].Shares[c] + 1 - held[c]]);

                foreach (var qty in targets.Where(q => q > 0 && q <= cap).Distinct())
                {
                    double gain = (Heuristics.BonusAt(game, me, c, held[c] + qty) - now) * bonusWeight;
                    if (gain <= 0) continue;
                    double score = gain / ((double)qty * company.Value) + ratio[c];
                    if (score > best.Score) best = (c, qty, score);
                }
            }
            if (best.Company < 0) break;

            orders.Add(new TradeOrder(TradeAction.Buy, best.Company, best.Qty));
            held[best.Company] += best.Qty;
            cash -= best.Qty * game.Companies[best.Company].Value;
            if (allowance > 0) allowance -= best.Qty;
        }
    }
}
