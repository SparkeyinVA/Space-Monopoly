namespace SpaceMonopoly.Core.Ai;

public enum AiLevel { Easy, Medium, Hard }

/// <summary>
/// Built-in computer opponent. Strategy follows the hints in the original documentation:
/// buy cheap companies that can grow next to circles, build companies close together so they merge,
/// and move money out of companies that have stopped growing.
/// </summary>
/// <remarks>
/// Easy mostly plays at random. Medium judges each square by its immediate result and never sells.
/// Hard simulates every square plus its follow-up trades and compares itself against the leading opponent.
/// </remarks>
public sealed class RuleBasedAi : IAiPlayer
{
    private readonly Random _rng;

    public RuleBasedAi(AiLevel level, int? seed = null)
    {
        Level = level;
        _rng = seed is int s ? new Random(s) : new Random();
    }

    public AiLevel Level { get; }

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

        var legal = game.LegalChoices();
        if (legal.Count == 0) throw new InvalidOperationException("No legal squares to choose from.");
        int me = game.CurrentPlayerIndex;

        if (Level == AiLevel.Easy && _rng.NextDouble() < 0.6)
            return legal[_rng.Next(legal.Count)];

        Pos best = legal[0];
        double bestScore = double.NegativeInfinity;
        foreach (var square in legal)
        {
            var sim = game.Clone();
            sim.PlaceSquare(square);

            double score = Level switch
            {
                AiLevel.Easy => (double)sim.NetWorth(me),
                AiLevel.Medium => Heuristics.RelativeOutlook(sim, me, opponentWeight: 0.25),
                _ => ScoreAfterTrades(sim, me),
            };
            // Small random tie-breaker so equal squares aren't always picked in the same order.
            score += _rng.NextDouble() * 0.01;

            if (score > bestScore)
            {
                bestScore = score;
                best = square;
            }
        }
        return best;
    }

    private double ScoreAfterTrades(Game sim, int me)
    {
        TradeExecutor.Apply(sim, PlanTrades(sim));
        return Heuristics.RelativeOutlook(sim, me, opponentWeight: 0.5);
    }

    public IReadOnlyList<TradeOrder> PlanTrades(Game game)
    {
        var existing = game.Companies.Where(c => c.Exists).ToList();
        if (existing.Count == 0) return [];
        return Level switch
        {
            AiLevel.Easy => PlanEasyTrades(game, existing),
            AiLevel.Medium => PlanInvestment(game, sellThreshold: null),
            _ => PlanInvestment(game, sellThreshold: 0.6),
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
    /// Sells holdings that are growing much slower than the best company (Hard only), then spends
    /// as much as possible on the best expected return, then the next best with what's left.
    /// </summary>
    private static List<TradeOrder> PlanInvestment(Game game, double? sellThreshold)
    {
        var orders = new List<TradeOrder>();
        var me = game.CurrentPlayer;
        var ratio = Heuristics.ReturnPerUnit(game, game.CurrentWorld);
        // In Modern, cash earns interest, so only companies expected to beat it are worth buying.
        double interest = Heuristics.InterestReturn(game, game.CurrentPlayerIndex);
        var ranked = Enumerable.Range(0, game.Companies.Length)
            .Where(c => game.Companies[c].Exists && ratio[c] > interest)
            .OrderByDescending(c => ratio[c])
            .ToList();
        if (ranked.Count == 0) return orders;

        double bestRatio = ratio[ranked[0]];
        var cash = me.Money;

        if (sellThreshold is double threshold)
        {
            for (int c = 0; c < game.Companies.Length; c++)
            {
                if (!game.Companies[c].Exists || me.Shares[c] == 0 || c == ranked[0]) continue;
                if (ratio[c] < bestRatio * threshold)
                {
                    orders.Add(new TradeOrder(TradeAction.Sell, c, 0));
                    cash += me.Shares[c] * game.Companies[c].Value;
                }
            }
        }

        // Modern caps how many shares can be bought in a turn and how many each company has left.
        var allowance = game.BuyAllowanceLeft;
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
}
