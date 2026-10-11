namespace SpaceMonopoly.Core;

/// <summary>
/// Modern (S15): after a merger, a holder of the company taken over may turn what they were paid (for their shares
/// plus any bonus) into shares of the survivor, at the survivor's price after the whole move.
/// </summary>
/// <param name="Owed">What the player was paid for the absorbed shares plus any majority bonus, across this move.</param>
public sealed record ConversionOffer(int Player, int Survivor, BigInteger Owed)
{
    /// <summary>The most survivor shares this money buys at the given price.</summary>
    public BigInteger MostShares(long price) => price > 0 ? Owed / price : 0;
}

/// <summary>Modern: what was converted when a merger's conversion offers were settled.</summary>
public sealed record ConversionEvent(int Survivor, long Price, IReadOnlyList<(int Player, BigInteger Shares)> Converted) : GameEvent;

public sealed partial class Game
{
    private List<ConversionOffer> _offers = [];

    /// <summary>
    /// Conversion offers from the merger just played (Modern). They must be settled with <see cref="SettleConversions"/>
    /// before the turn ends; ending the turn settles any left with no conversion.
    /// </summary>
    public IReadOnlyList<ConversionOffer> PendingConversions => _offers;

    private void CopyConversions(Game other) => _offers = [.. other._offers];

    /// <summary>Records the money each holder of an absorbed company was paid, so they can convert it after the move.</summary>
    private void OfferConversion(int survivor, IEnumerable<MergerRow> rows)
    {
        if (!IsModern) return;
        foreach (var row in rows.Where(r => r.Total > 0))
        {
            int existing = _offers.FindIndex(o => o.Player == row.Player && o.Survivor == survivor);
            if (existing >= 0) _offers[existing] = _offers[existing] with { Owed = _offers[existing].Owed + row.Total };
            else _offers.Add(new ConversionOffer(row.Player, survivor, row.Total));
        }
    }

    /// <summary>After a three-way merger an earlier survivor can itself be taken over; its offers move to the final survivor.</summary>
    private void RedirectOffers(int absorbed, int survivor)
    {
        for (int i = 0; i < _offers.Count; i++)
            if (_offers[i].Survivor == absorbed) _offers[i] = _offers[i] with { Survivor = survivor };
    }

    /// <summary>
    /// Settles the pending offers: each player gets up to the survivor shares they ask for, paid from what they were owed,
    /// at the survivor's current price. Conversions don't count toward the buy limit. If more shares are asked for than
    /// are left, they're shared out in proportion to the money each player is owed (rounded down). Returns what happened.
    /// </summary>
    public ConversionEvent? SettleConversions(IReadOnlyDictionary<int, BigInteger> requested)
    {
        if (_offers.Count == 0) return null;
        var offers = _offers;
        _offers = [];

        int survivor = offers[0].Survivor;   // every offer from one move is for the same final survivor
        var company = Companies[survivor];
        if (!company.Exists || company.Value <= 0) return null;
        long price = company.Value;

        var wanted = offers.Select(o => (o.Player, o.Owed,
            Shares: BigInteger.Max(0, BigInteger.Min(BigInteger.Min(requested.GetValueOrDefault(o.Player), o.MostShares(price)),
                _players[o.Player].Money / price)))).ToList();
        var total = wanted.Aggregate(BigInteger.Zero, (sum, w) => sum + w.Shares);
        var left = SharesLeft(survivor);
        if (left >= 0 && total > left)
        {
            var owedTotal = wanted.Where(w => w.Shares > 0).Aggregate(BigInteger.Zero, (sum, w) => sum + w.Owed);
            wanted = wanted.Select(w => w with { Shares = w.Shares == 0 ? 0 : BigInteger.Min(w.Shares, left * w.Owed / owedTotal) }).ToList();
        }

        var converted = new List<(int Player, BigInteger Shares)>();
        foreach (var (player, _, shares) in wanted.Where(w => w.Shares > 0))
        {
            _players[player].Shares[survivor] += shares;
            _players[player].Money -= shares * price;
            converted.Add((player, shares));
        }
        return new ConversionEvent(survivor, price, converted);
    }
}
