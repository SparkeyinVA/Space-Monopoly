namespace SpaceMonopoly.Core;

/// <summary>A secret goal dealt to each player at the start of a Modern game and revealed at the end (S7).</summary>
public enum SecretGoal
{
    /// <summary>Be the largest holder of the biggest company (by squares).</summary>
    BiggestCompany,

    /// <summary>A reward for each company you founded that still exists at the end.</summary>
    Founder,

    /// <summary>Hold the most cash.</summary>
    MostCash,

    /// <summary>Be the largest holder in three or more companies.</summary>
    Tycoon,

    /// <summary>Hold shares in every company in play.</summary>
    Diversified,

    /// <summary>Make the most mergers (play the squares that merge companies).</summary>
    MostMergers,
}

public static class SecretGoals
{
    public const long BiggestCompanyReward = 20_000;
    public const long FounderRewardEach = 4_000;
    public const long MostCashReward = 15_000;
    public const long TycoonReward = 20_000;
    public const int TycoonCompanies = 3;
    public const long DiversifiedReward = 15_000;
    public const long MostMergersReward = 15_000;

    public static string Description(SecretGoal goal) => goal switch
    {
        SecretGoal.BiggestCompany => $"Be the largest holder of the biggest company (by squares): {BiggestCompanyReward:N0}.",
        SecretGoal.Founder => $"{FounderRewardEach:N0} for each company you founded that still exists at the end.",
        SecretGoal.MostCash => $"Hold the most cash at the end: {MostCashReward:N0}.",
        SecretGoal.Tycoon => $"Be the largest holder in {TycoonCompanies} or more companies: {TycoonReward:N0}.",
        SecretGoal.Diversified => $"Hold shares in every company in play at the end: {DiversifiedReward:N0}.",
        SecretGoal.MostMergers => $"Make the most mergers (play the squares that merge companies): {MostMergersReward:N0}.",
        _ => goal.ToString(),
    };
}

/// <summary>Modern, at the end of the game: a company's two largest holders are paid 10× and 5× its share price (S7).</summary>
public sealed record FinalPayoutEvent(int Company, long Price, IReadOnlyList<Payout> Payouts) : GameEvent;

/// <summary>Modern, at the end of the game: a player's secret goal is revealed, with the reward if they met it (0 if not).</summary>
public sealed record SecretGoalEvent(int Player, SecretGoal Goal, BigInteger Reward) : GameEvent;

/// <summary>Modern comeback mechanics (S7): an extra square for last place, final payouts and secret goals.</summary>
public sealed partial class Game
{
    private SecretGoal[] _goals = [];
    private int[] _founders = [];   // who founded each company (-1 if none)
    private int[] _mergersMade = [];
    private bool _finished;

    /// <summary>What happened when the game ended: final payouts and secret goals (Modern). Empty before then.</summary>
    public IReadOnlyList<GameEvent> GameEndEvents { get; private set; } = [];

    /// <summary>A player's secret goal (Modern). Only show players their own until the game ends.</summary>
    public SecretGoal? GoalOf(int player) => IsModern ? _goals[player] : null;

    /// <summary>Squares a player holds: one more than usual while they are alone in last place.</summary>
    private int HandSizeFor(int player)
    {
        if (_players.Length < 2) return ModernRules.HandSize;
        var worth = NetWorth(player);
        bool aloneLast = Enumerable.Range(0, _players.Length).All(i => i == player || NetWorth(i) > worth);
        return ModernRules.HandSize + (aloneLast ? 1 : 0);
    }

    private void InitComebacks()
    {
        // Deal the goals from a shuffled set, so players get different goals while there are enough to go round.
        var pool = new List<SecretGoal>();
        _goals = new SecretGoal[_players.Length];
        for (int i = 0; i < _players.Length; i++)
        {
            if (pool.Count == 0)
            {
                pool.AddRange(Enum.GetValues<SecretGoal>());
                for (int k = pool.Count - 1; k > 0; k--)
                {
                    int j = _rng.Next(k + 1);
                    (pool[k], pool[j]) = (pool[j], pool[k]);
                }
            }
            _goals[i] = pool[^1];
            pool.RemoveAt(pool.Count - 1);
        }
        _founders = Enumerable.Repeat(-1, Spec.Companies).ToArray();
        _mergersMade = new int[_players.Length];
    }

    private void CopyComebacks(Game other)
    {
        _goals = (SecretGoal[])other._goals.Clone();
        _founders = (int[])other._founders.Clone();
        _mergersMade = (int[])other._mergersMade.Clone();
        _finished = other._finished;
        GameEndEvents = other.GameEndEvents;
    }

    private void RecordFounder(int company)
    {
        if (IsModern) _founders[company] = CurrentPlayerIndex;
    }

    private void RecordMerger()
    {
        if (IsModern) _mergersMade[CurrentPlayerIndex]++;
    }

    /// <summary>Ends the game: in Modern, pays the final payouts and secret goals (once).</summary>
    private void Finish()
    {
        Phase = GamePhase.Over;
        _choices = [];
        if (!IsModern || _finished) return;
        _finished = true;

        var events = new List<GameEvent>();
        var live = Companies.Where(c => c.Exists).ToList();

        // Final payouts: every company still in play pays its two largest holders, as at a merger.
        foreach (var c in live)
        {
            var bonuses = MajorityBonuses(Enumerable.Range(0, _players.Length).Select(i => (i, _players[i].Shares[c.Index])), c.Value);
            if (bonuses.Count == 0) continue;
            foreach (var (player, amount) in bonuses) _players[player].Money += amount;
            events.Add(new FinalPayoutEvent(c.Index, c.Value, bonuses.Select(b => new Payout(b.Key, b.Value)).ToList()));
        }

        // Secret goals are judged after the payouts, so "most cash" counts them.
        var rewards = Enumerable.Range(0, _players.Length).Select(i => GoalReward(i, live)).ToArray();
        for (int i = 0; i < _players.Length; i++)
        {
            _players[i].Money += rewards[i];
            events.Add(new SecretGoalEvent(i, _goals[i], rewards[i]));
        }
        GameEndEvents = events;
    }

    private BigInteger LargestHolders(Company c)
    {
        BigInteger most = 0;
        foreach (var p in _players) most = BigInteger.Max(most, p.Shares[c.Index]);
        return most;
    }

    private bool IsLargestHolder(int player, Company c)
    {
        var mine = _players[player].Shares[c.Index];
        return mine > 0 && mine == LargestHolders(c);
    }

    private BigInteger GoalReward(int player, List<Company> live)
    {
        switch (_goals[player])
        {
            case SecretGoal.BiggestCompany:
                var biggest = live.OrderByDescending(c => Board.CompanySize(c.Index)).ThenBy(c => c.Index).FirstOrDefault();
                return biggest is not null && IsLargestHolder(player, biggest) ? SecretGoals.BiggestCompanyReward : 0;

            case SecretGoal.Founder:
                return SecretGoals.FounderRewardEach * live.Count(c => _founders[c.Index] == player);

            case SecretGoal.MostCash:
                var cash = _players[player].Money;
                return cash > 0 && _players.All(p => p.Money <= cash) ? SecretGoals.MostCashReward : 0;

            case SecretGoal.Tycoon:
                return live.Count(c => IsLargestHolder(player, c)) >= SecretGoals.TycoonCompanies ? SecretGoals.TycoonReward : 0;

            case SecretGoal.Diversified:
                return live.Count > 0 && live.All(c => _players[player].Shares[c.Index] > 0) ? SecretGoals.DiversifiedReward : 0;

            case SecretGoal.MostMergers:
                int made = _mergersMade[player];
                return made > 0 && _mergersMade.All(m => m <= made) ? SecretGoals.MostMergersReward : 0;
        }
        return 0;
    }
}
