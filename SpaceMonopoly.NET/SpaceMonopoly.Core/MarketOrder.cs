namespace SpaceMonopoly.Core;

/// <summary>What a market order would do, and any problems with it, by company (S16).</summary>
/// <param name="AllowanceAfter">Modern: shares the player could still buy this turn afterwards; -1 when there's no limit.</param>
public sealed record MarketCheck(
    BigInteger Cost,
    BigInteger Proceeds,
    BigInteger CashAfter,
    BigInteger AllowanceAfter,
    IReadOnlyDictionary<int, string> Problems)
{
    public bool Ok => Problems.Count == 0;
}

/// <summary>
/// Several buys and sells for the current player, checked together and carried out at once (S16). Sells are applied
/// before buys, so money from a sale can pay for a purchase in the same order.
/// </summary>
public sealed class MarketOrder
{
    public Dictionary<int, BigInteger> Buys { get; } = [];
    public Dictionary<int, BigInteger> Sells { get; } = [];

    public BigInteger BuyOf(int company) => Buys.GetValueOrDefault(company);
    public BigInteger SellOf(int company) => Sells.GetValueOrDefault(company);

    public bool IsEmpty => Buys.Values.All(q => q == 0) && Sells.Values.All(q => q == 0);

    public void Clear()
    {
        Buys.Clear();
        Sells.Clear();
    }

    public MarketCheck Check(Game game)
    {
        var me = game.CurrentPlayer;
        var problems = new Dictionary<int, string>();
        BigInteger cost = 0, proceeds = 0, bought = 0;

        for (int c = 0; c < game.Companies.Length; c++)
        {
            var company = game.Companies[c];
            var buy = BuyOf(c);
            var sell = SellOf(c);
            if (buy == 0 && sell == 0) continue;
            if (!company.Exists) { problems[c] = "This company no longer exists."; continue; }
            if (buy < 0 || sell < 0) { problems[c] = "Amounts can't be negative."; continue; }
            if (sell > me.Shares[c]) problems[c] = $"You hold only {Currency.Number(me.Shares[c])}.";
            var left = game.SharesLeft(c);
            if (left >= 0 && buy > left + sell) problems[c] = $"Only {Currency.Number(left + sell)} shares are left.";
            cost += buy * company.Value;
            proceeds += sell * company.Value;
            bought += buy;
        }

        var allowance = game.BuyAllowanceLeft;
        if (allowance >= 0 && bought > allowance)
            foreach (var c in Buys.Where(b => b.Value > 0).Select(b => b.Key))
                problems.TryAdd(c, $"You can buy only {allowance} more shares this turn (this order buys {bought}).");

        var cashAfter = me.Money + proceeds - cost;
        if (cashAfter < 0)
            foreach (var c in Buys.Where(b => b.Value > 0).Select(b => b.Key))
                problems.TryAdd(c, $"Not enough cash: {Currency.Default.Format(-cashAfter)} short.");

        return new MarketCheck(cost, proceeds, cashAfter, allowance < 0 ? BigInteger.MinusOne : allowance - bought, problems);
    }

    /// <summary>The most this company's buy could be, given the cash, allowance and shares the rest of the order leaves.</summary>
    public BigInteger MaxBuy(Game game, int company)
    {
        var c = game.Companies[company];
        if (!c.Exists || c.Value <= 0) return 0;
        var others = new MarketOrder();
        foreach (var (k, v) in Buys) if (k != company) others.Buys[k] = v;
        foreach (var (k, v) in Sells) others.Sells[k] = v;
        var check = others.Check(game);

        var most = BigInteger.Max(0, check.CashAfter) / c.Value;
        if (check.AllowanceAfter >= 0) most = BigInteger.Min(most, BigInteger.Max(0, check.AllowanceAfter));
        var left = game.SharesLeft(company);
        if (left >= 0) most = BigInteger.Min(most, left + SellOf(company));
        return BigInteger.Max(0, most);
    }

    /// <summary>Carries out the order: every sale, then every purchase. Throws if the order doesn't pass its check.</summary>
    public List<TradeEvent> Execute(Game game)
    {
        var check = Check(game);
        if (!check.Ok) throw new InvalidOperationException(string.Join(" ", check.Problems.Values.Distinct()));
        var done = new List<TradeEvent>();
        foreach (var (c, q) in Sells.Where(s => s.Value > 0).OrderBy(s => s.Key)) done.Add(game.Sell(c, q));
        foreach (var (c, q) in Buys.Where(b => b.Value > 0).OrderBy(b => b.Key)) done.Add(game.Buy(c, q));
        Clear();
        return done;
    }
}
