namespace SpaceMonopoly.Core.Ai;

public enum TradeAction { Buy, Sell }

/// <summary>A trade a computer player wants to make. A quantity of 0 means "as many as possible" (buy max / sell all).</summary>
public sealed record TradeOrder(TradeAction Action, int Company, BigInteger Quantity);

/// <summary>A computer opponent. Each turn the game asks for a square, plays it, then asks for trades.</summary>
public interface IAiPlayer
{
    Task<Pos> ChooseSquareAsync(Game game, CancellationToken cancellationToken);

    Task<IReadOnlyList<TradeOrder>> PlanTradesAsync(Game game, CancellationToken cancellationToken);

    /// <summary>Optional remark about the last move, shown in the game log.</summary>
    string? LastComment { get; }
}

public static class TradeExecutor
{
    /// <summary>Applies orders for the current player, clamping quantities and skipping anything invalid.</summary>
    public static List<TradeEvent> Apply(Game game, IEnumerable<TradeOrder> orders)
    {
        var done = new List<TradeEvent>();
        foreach (var order in orders)
        {
            if (order.Company < 0 || order.Company >= game.Companies.Length || !game.Companies[order.Company].Exists) continue;

            if (order.Action == TradeAction.Sell)
            {
                var held = game.CurrentPlayer.Shares[order.Company];
                var qty = order.Quantity <= 0 ? held : BigInteger.Min(order.Quantity, held);
                if (qty > 0) done.Add(game.Sell(order.Company, qty));
            }
            else
            {
                var max = game.MaxBuy(order.Company);
                var qty = order.Quantity <= 0 ? max : BigInteger.Min(order.Quantity, max);
                if (qty > 0) done.Add(game.Buy(order.Company, qty));
            }
        }
        return done;
    }
}

public static class AiFactory
{
    public static IAiPlayer Create(PlayerKind kind, ClaudeCodeSettings claudeSettings, int? seed = null) => kind switch
    {
        PlayerKind.ComputerEasy => new RuleBasedAi(AiLevel.Easy, seed),
        PlayerKind.ComputerMedium => new RuleBasedAi(AiLevel.Medium, seed),
        PlayerKind.ComputerHard => new RuleBasedAi(AiLevel.Hard, seed),
        PlayerKind.Claude => new ClaudeCodeAi(claudeSettings, new RuleBasedAi(AiLevel.Hard, seed)),
        _ => throw new ArgumentException($"{kind} is not a computer player.", nameof(kind)),
    };
}
