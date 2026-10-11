namespace SpaceMonopoly.Core.Ai;

/// <summary>
/// Estimates of how much each company's shares are likely to grow before the game ends.
/// Share prices never fall (splits double your shares), so the question is only where money grows fastest.
/// </summary>
internal static class Heuristics
{
    /// <summary>Probability that someone eventually plays a particular empty square next to a company.</summary>
    public static double PlayProbability(Game game, Board board)
    {
        int empty = board.CountOf(Board.Empty);
        if (empty == 0) return 0;
        int goes = game.RemainingGoes;
        double offeredPerGo = Math.Min(1.0, (double)Game.ChoiceCount / empty);
        double everOffered = 1 - Math.Pow(1 - offeredPerGo, goes);
        // Squares that grow a company are popular picks when offered, but not guaranteed.
        return everOffered * 0.45;
    }

    /// <summary>Expected rise in value per share for each company in a world.</summary>
    public static double[] GrowthPerShare(Game game, World world)
    {
        var board = world.Board;
        var companies = world.Companies;
        var growth = new double[companies.Length];
        double p = PlayProbability(game, board);
        if (p == 0) return growth;

        var sizes = Enumerable.Range(0, companies.Length).Select(board.CompanySize).ToArray();

        foreach (var cell in board.AllPositions())
        {
            if (board[cell] != Board.Empty) continue;
            var s = board.Survey(cell);
            if (s.Companies.Count == 0) continue;

            if (s.NearBlackHole) continue;
            double squareGain = Game.SquareValue + Game.CircleBonus * s.Circles * (s.NearNebula ? 2 : 1) + Game.DiamondBonus * s.Diamonds;

            if (s.Companies.Count == 1)
            {
                growth[s.Companies[0]] += squareGain * p;
                continue;
            }

            // A merger square: work out who survives, as the engine would.
            int survivor = s.Companies.OrderByDescending(c => sizes[c]).ThenBy(c => c).First();
            double absorbedTotal = s.Companies.Where(c => c != survivor).Sum(c => (double)companies[c].Value)
                                   / (game.IsModern ? ModernRules.MergerPriceDivisor : 1);
            growth[survivor] += (absorbedTotal + squareGain) * p;
            foreach (int c in s.Companies.Where(c => c != survivor))
            {
                // Classic: each absorbed share is paid out at its own price and also becomes a survivor share,
                // so the holder gains a whole survivor share at its post-merger price.
                // Modern: it is only bought out at its own price, so the share itself gains nothing
                // (the majority bonus is valued separately, by who holds the most).
                if (game.IsModern) continue;
                double survivorAfter = companies[survivor].Value + absorbedTotal + squareGain;
                growth[c] += survivorAfter * p;
            }
        }

        for (int c = 0; c < companies.Length; c++)
            if (!companies[c].Exists) growth[c] = 0;
        return growth;
    }

    /// <summary>
    /// Modern: how next round's announced event card is expected to change each company's share price,
    /// as a fraction of its price (for example -0.25 for the target of a Pirate Raid).
    /// </summary>
    public static double[] NextEventEffect(Game game)
    {
        var effect = new double[game.Companies.Length];
        var live = game.Companies.Where(c => c.Exists).ToList();
        if (game.NextEvent is not EventCard card || live.Count == 0) return effect;

        switch (card)
        {
            case EventCard.PirateRaid:
                effect[live.OrderByDescending(c => c.Value).ThenBy(c => c.Index).First().Index] = -EventCards.PirateRaidPercent / 100.0;
                break;
            case EventCard.MarketBoom:
                foreach (var c in live) effect[c.Index] = EventCards.MarketBoomPercent / 100.0;
                break;
            case EventCard.AsteroidStrike:
                var big = live.Where(c => game.Board.CompanySize(c.Index) > 1).ToList();
                foreach (var c in big) effect[c.Index] = -(double)EventCards.AsteroidLoss / c.Value / big.Count;
                break;
            case EventCard.InsiderScandal:
                int leader = Enumerable.Range(0, game.Players.Count).OrderByDescending(game.NetWorth).First();
                var held = live.Where(c => game.Players[leader].Shares[c.Index] > 0)
                    .OrderByDescending(c => game.Players[leader].Shares[c.Index] * c.Value).FirstOrDefault();
                if (held is not null) effect[held.Index] = -EventCards.ScandalPercent / 100.0;
                break;
        }
        return effect;
    }

    /// <summary>
    /// Modern: the majority bonus a player would collect from a company if they held <paramref name="shares"/>.
    /// Every company pays its two largest holders (10x and 5x its price) either when it is taken over or at
    /// the end of the game, so this is what the stake is worth on top of the shares themselves.
    /// </summary>
    public static double BonusAt(Game game, int player, int company, BigInteger shares)
    {
        var c = game.Companies[company];
        if (!game.IsModern || !c.Exists || shares < ModernRules.MinSharesForBonus) return 0;
        var holdings = Enumerable.Range(0, game.Players.Count)
            .Select(i => (i, i == player ? shares : game.Players[i].Shares[company])).ToList();
        double merger = (double)Game.MajorityBonuses(holdings, ModernRules.BonusPrice(c)).GetValueOrDefault(player);

        // At the end only companies of 4+ squares pay, at half the merger bonus, and late buying doesn't count (F7).
        double final = 0;
        if (game.Board.CompanySize(company) >= ModernRules.FinalPayoutMinSquares - 1 && !game.LateBuying)
            final = (double)Game.FinalPayouts(holdings, ModernRules.BonusPrice(c)).GetValueOrDefault(player);

        double absorbed = MayBeAbsorbed(game, company) ? 0.5 : 0.1;
        return absorbed * merger + (1 - absorbed) * final;
    }

    /// <summary>True if an empty square joins this company to one at least as big, so it could be taken over.</summary>
    private static bool MayBeAbsorbed(Game game, int company)
    {
        var board = game.Board;
        int size = board.CompanySize(company);
        foreach (var p in board.AllPositions())
        {
            if (board[p] != Board.Empty) continue;
            var s = board.Survey(p);
            if (s.Companies.Contains(company) && s.Companies.Any(o => o != company && board.CompanySize(o) >= size)) return true;
        }
        return false;
    }

    /// <summary>Modern: the majority bonuses a player's current holdings are on course to collect.</summary>
    public static double ExpectedBonuses(Game game, int player)
    {
        if (!game.IsModern) return 0;
        double total = 0;
        for (int c = 0; c < game.Companies.Length; c++)
            total += BonusAt(game, player, c, game.Players[player].Shares[c]);
        return total;
    }

    /// <summary>Modern: what a unit of cash held to the end of the game earns in interest.</summary>
    public static double InterestReturn(Game game, int player)
    {
        if (!game.IsModern) return 0;
        int turnsLeft = Math.Max(0, game.Rounds - game.Turn - (player < game.CurrentPlayerIndex ? 1 : 0));
        return Math.Pow(1 + ModernRules.InterestPercent / 100.0, turnsLeft) - 1;
    }

    /// <summary>Expected growth per unit of money invested in each company.</summary>
    public static double[] ReturnPerUnit(Game game, World world)
    {
        var growth = GrowthPerShare(game, world);
        var ratio = new double[growth.Length];
        for (int c = 0; c < growth.Length; c++)
        {
            var company = world.Companies[c];
            ratio[c] = company.Exists && company.Value > 0 ? growth[c] / company.Value : 0;

            // S19: once a company splits, each square's flat price rise applies to twice as many shares, so a company
            // close to splitting is worth more to hold (unless growth is halved after a split).
            if (game.IsModern && !ModernRules.GrowthHalvesAfterSplit && company.Exists && ModernRules.NextSplit(company.Splits) is var (squares, price)
                && world.Board.CompanySize(c) >= squares - 1 && company.Value >= price * 4 / 5)
                ratio[c] *= 1.5;
        }
        return ratio;
    }

    /// <summary>Net worth plus the expected growth of a player's holdings and cash.</summary>
    public static double Outlook(Game game, int player)
    {
        var world = game.WorldFor(player);
        var growth = GrowthPerShare(game, world);
        var ratio = ReturnPerUnit(game, world);
        var pl = game.Players[player];

        double expected = 0;
        for (int c = 0; c < growth.Length; c++)
            expected += (double)pl.Shares[c] * growth[c];

        // Cash can still be invested at the best available return, or in Modern earn interest.
        double bestRatio = ratio.Max();
        expected += (double)pl.Money * Math.Max(bestRatio * 0.8, InterestReturn(game, player));

        return (double)game.NetWorth(player) + expected + ExpectedBonuses(game, player);
    }

    /// <summary>How well a player is doing compared with the strongest opponent.</summary>
    public static double RelativeOutlook(Game game, int player, double opponentWeight)
    {
        double mine = Outlook(game, player);
        if (opponentWeight == 0 || game.Players.Count == 1 || !game.SharedBoard) return mine;
        double best = Enumerable.Range(0, game.Players.Count)
            .Where(i => i != player)
            .Max(i => Outlook(game, i));
        return mine - opponentWeight * best;
    }
}
