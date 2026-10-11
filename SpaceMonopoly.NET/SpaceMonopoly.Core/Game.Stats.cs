namespace SpaceMonopoly.Core;

/// <summary>The single biggest merger payout of the game, to one player (for the end-of-game awards, S10).</summary>
public sealed record MergerRecord(int Player, int Survivor, int Absorbed, BigInteger Amount, int Round);

/// <summary>Statistics kept for the end-of-game screen and achievements (S10).</summary>
public sealed partial class Game
{
    private List<BigInteger[]> _history = [];
    private int[] _founded = [];
    private BigInteger[] _bonusesCollected = [];

    /// <summary>Each player's net worth at the start and at the end of every round (and when the game ended).</summary>
    public IReadOnlyList<IReadOnlyList<BigInteger>> NetWorthHistory => _history;

    /// <summary>How many companies each player founded this game.</summary>
    public IReadOnlyList<int> CompaniesFounded => _founded;

    /// <summary>Majority bonuses each player collected at mergers this game (Modern).</summary>
    public IReadOnlyList<BigInteger> BonusesCollected => _bonusesCollected;

    /// <summary>The biggest single merger payout (shares bought back plus bonus) this game, or null.</summary>
    public MergerRecord? BiggestMerger { get; private set; }

    private void InitStats()
    {
        _founded = new int[_players.Length];
        _bonusesCollected = new BigInteger[_players.Length];
        _history = [Snapshot()];
    }

    private void CopyStats(Game other)
    {
        _history = other._history.Select(h => (BigInteger[])h.Clone()).ToList();
        _founded = (int[])other._founded.Clone();
        _bonusesCollected = (BigInteger[])other._bonusesCollected.Clone();
        BiggestMerger = other.BiggestMerger;
    }

    private BigInteger[] Snapshot() => Enumerable.Range(0, _players.Length).Select(NetWorth).ToArray();

    /// <summary>Called when a round ends (and when the game ends) to record everyone's net worth.</summary>
    private void RecordRound() => _history.Add(Snapshot());

    private void RecordFounding() => _founded[CurrentPlayerIndex]++;

    private void RecordMergerPayouts(int survivor, int absorbed, IEnumerable<MergerRow> rows)
    {
        foreach (var row in rows)
        {
            _bonusesCollected[row.Player] += row.Bonus;
            if (row.Total > 0 && (BiggestMerger is null || row.Total > BiggestMerger.Amount))
                BiggestMerger = new MergerRecord(row.Player, survivor, absorbed, row.Total, Turn + 1);
        }
    }

    /// <summary>The player in last place at the halfway round, or -1 if the game hasn't got that far.</summary>
    public int LastAtHalfway()
    {
        int half = Math.Max(1, Rounds / 2);
        if (_history.Count <= half) return -1;
        var worth = _history[half];
        return Enumerable.Range(0, worth.Length).OrderBy(i => worth[i]).First();
    }
}
