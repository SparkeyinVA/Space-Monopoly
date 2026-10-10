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

            double squareGain = Game.SquareValue + Game.CircleBonus * s.Circles + Game.DiamondBonus * s.Diamonds;

            if (s.Companies.Count == 1)
            {
                growth[s.Companies[0]] += squareGain * p;
                continue;
            }

            // A merger square: work out who survives, as the engine would.
            int survivor = s.Companies.OrderByDescending(c => sizes[c]).ThenBy(c => c).First();
            double absorbedTotal = s.Companies.Where(c => c != survivor).Sum(c => (double)companies[c].Value);
            growth[survivor] += (absorbedTotal + squareGain) * p;
            foreach (int c in s.Companies.Where(c => c != survivor))
            {
                // Each absorbed share is paid out at its own price and also becomes a survivor share,
                // so the holder gains a whole survivor share at its post-merger price.
                double survivorAfter = companies[survivor].Value + absorbedTotal + squareGain;
                growth[c] += survivorAfter * p;
            }
        }

        for (int c = 0; c < companies.Length; c++)
            if (!companies[c].Exists) growth[c] = 0;
        return growth;
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

        // Cash can still be invested at the best available return.
        double bestRatio = ratio.Max();
        expected += (double)pl.Money * bestRatio * 0.8;

        return (double)game.NetWorth(player) + expected;
    }

    /// <summary>How well a player is doing compared with the strongest opponent.</summary>
    public static double RelativeOutlook(Game game, int player, double opponentWeight)
    {
        double mine = Outlook(game, player);
        if (opponentWeight == 0 || game.Players.Count == 1 || game.Type == GameType.Tournament) return mine;
        double best = Enumerable.Range(0, game.Players.Count)
            .Where(i => i != player)
            .Max(i => Outlook(game, i));
        return mine - opponentWeight * best;
    }
}
