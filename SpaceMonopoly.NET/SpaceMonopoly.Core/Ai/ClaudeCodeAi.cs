using System.Text;
using System.Text.Json;

namespace SpaceMonopoly.Core.Ai;

/// <summary>
/// A computer player that asks Claude (through Claude Code) for each move. One request per turn covers
/// both the square and the trades. If Claude can't be reached or gives an unusable answer, the turn is
/// played by the Hard built-in strategy instead and the reason is reported in <see cref="LastComment"/>.
/// </summary>
public sealed class ClaudeCodeAi : IAiPlayer
{
    internal const string SystemPrompt = """
        You are playing Space Monopoly, a board game about buying and selling shares, against other players.
        Your goal is to finish with the highest net worth (cash plus shares at their final price).

        Rules:
        - Each turn you must pick one of the numbered squares offered on the map.
        - A square next to a circle (o) or star (*) but not next to a company founds a new company, if the
          board's company limit hasn't been reached. The founder gets 5 free shares plus cash equal to the starting share price.
        - A square next to exactly one company grows it. The share price rises by 100, plus 500 for each circle
          next to the new square, plus 100 for each star next to it (stars get absorbed into the company).
          Every shareholder is paid a dividend of (shares held x price rise).
        - A square next to two or more companies merges them. The bigger company survives (ties go to the earlier
          letter). Holders of the absorbed company are paid its share price in cash for each share AND get the
          same number of shares in the survivor. The survivor's price rises by the absorbed company's price.
        - Any other square just places a star.
        - Share prices of 3,200 or more split two-for-one: the price halves and everyone's shares double.
        - Share prices never fall, so cash that isn't invested earns nothing.
        - After placing your square you may buy and sell shares at the current prices. Trades happen in order.

        Strategy hints from the original game's author:
        - Cheap companies (about 200) next to circles grow fastest relative to their price.
        - Found companies close together so they can merge later; mergers make holders of the smaller company rich.
        - Be careful merging when an opponent holds more of the smaller company than you do.
        - Late in the game, move money out of companies that can't grow any more.

        Answer with JSON only, using the schema you're given. "choice" is the number of the square you pick.
        "trades" are executed after your square is placed, at the prices shown for that square's outcome.
        Use quantity 0 to mean "as many as possible" (buy max, or sell all). "comment" is one short sentence
        explaining your move, written for the other players to read.
        """;

    internal const string Schema = """
        {"type":"object","properties":{"choice":{"type":"integer","minimum":1,"maximum":5},"trades":{"type":"array","items":{"type":"object","properties":{"action":{"type":"string","enum":["buy","sell"]},"company":{"type":"string","enum":["A","B","C","D","E","F","G","H","I","J","K","L"]},"quantity":{"type":"integer","minimum":0}},"required":["action","company","quantity"],"additionalProperties":false}},"comment":{"type":"string"}},"required":["choice","trades","comment"],"additionalProperties":false}
        """;

    private readonly IClaudeRunner _runner;
    private readonly RuleBasedAi _fallback;
    private IReadOnlyList<TradeOrder>? _pendingTrades;
    private bool _usedFallback;

    public ClaudeCodeAi(ClaudeCodeSettings settings, RuleBasedAi fallback)
        : this(new ClaudeCodeRunner(settings), fallback)
    {
    }

    public ClaudeCodeAi(IClaudeRunner runner, RuleBasedAi fallback)
    {
        _runner = runner;
        _fallback = fallback;
    }

    public string? LastComment { get; private set; }

    public async Task<Pos> ChooseSquareAsync(Game game, CancellationToken cancellationToken)
    {
        _pendingTrades = null;
        _usedFallback = false;
        try
        {
            var answer = await _runner.RunAsync(SystemPrompt, BuildPrompt(game), Schema, cancellationToken);
            var (square, trades, comment) = ParseAnswer(game, answer);
            _pendingTrades = trades;
            LastComment = comment;
            return square;
        }
        catch (Exception ex) when (ex is ClaudeCodeException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            _usedFallback = true;
            LastComment = $"(Claude unavailable — {ex.Message} Playing this turn with the built-in Hard strategy.)";
            return _fallback.ChooseSquare(game);
        }
    }

    public Task<IReadOnlyList<TradeOrder>> PlanTradesAsync(Game game, CancellationToken cancellationToken)
    {
        if (_usedFallback || _pendingTrades is null)
            return Task.FromResult(_fallback.PlanTrades(game));
        var trades = _pendingTrades;
        _pendingTrades = null;
        return Task.FromResult(trades);
    }

    internal static (Pos Square, IReadOnlyList<TradeOrder> Trades, string Comment) ParseAnswer(Game game, JsonElement answer)
    {
        int choice = answer.GetProperty("choice").GetInt32();
        if (choice < 1 || choice > game.Choices.Count)
            throw new InvalidOperationException($"Claude picked square {choice}, which wasn't offered.");
        var square = game.Choices[choice - 1];
        if (game.ValidateChoice(square) is string why)
            throw new InvalidOperationException($"Claude picked square {choice}, which isn't allowed: {why}");

        var trades = new List<TradeOrder>();
        if (answer.TryGetProperty("trades", out var list) && list.ValueKind == JsonValueKind.Array)
        {
            foreach (var t in list.EnumerateArray())
            {
                string letter = t.GetProperty("company").GetString() ?? "";
                if (letter.Length != 1 || letter[0] < 'A' || letter[0] - 'A' >= game.Companies.Length) continue;
                var action = t.GetProperty("action").GetString() == "sell" ? TradeAction.Sell : TradeAction.Buy;
                var qty = BigInteger.TryParse(t.GetProperty("quantity").GetRawText(), out var q) && q > 0 ? q : BigInteger.Zero;
                trades.Add(new TradeOrder(action, letter[0] - 'A', qty));
            }
        }

        string comment = answer.TryGetProperty("comment", out var c) ? c.GetString() ?? "" : "";
        return (square, trades, comment.Trim());
    }

    /// <summary>Describes the position, including a preview of what each offered square would do.</summary>
    internal static string BuildPrompt(Game game)
    {
        var currency = Currency.Default; // Claude sees the default unit whatever the player has chosen.
        int me = game.CurrentPlayerIndex;
        var sb = new StringBuilder();

        sb.AppendLine($"You are {game.Players[me].Name}. Round {game.Turn + 1} of {game.Rounds} " +
                      $"({game.Type} game, {game.Spec.Width}x{game.Spec.Height} board, {game.Players.Count} players, " +
                      $"up to {game.Companies.Length} companies). Money is in Galactic Bucks (₲).");
        if (game.NoMoreCompanies)
            sb.AppendLine($"No more companies can be formed, so the game ends after round {game.Rounds}.");
        if (game.IsModern)
        {
            sb.AppendLine($"Modern game: everyone shares this board. The squares on offer are your private hand of {ModernRules.HandSize}; you draw a new one after playing.");
            sb.AppendLine($"Rules: growth raises the share price only (no cash dividends). You may buy at most {ModernRules.BuyLimitPerTurn} shares per turn " +
                          $"(you have {game.BuyAllowanceLeft} left); selling is unlimited. Each company has {ModernRules.SharesPerCompany} shares in total (doubled by each split). " +
                          $"Founding gives {ModernRules.FreeShares} free shares and no cash. In a merger, shares in the company taken over are paid out in cash at its price, " +
                          $"and its largest holder gets a bonus of {ModernRules.LargestHolderBonus}x its share price, the second-largest {ModernRules.SecondHolderBonus}x. " +
                          $"Cash earns {ModernRules.InterestPercent}% interest at the start of each of your turns, so holding cash can make sense.");
            if (game.CurrentEvent is EventCard now && game.NextEvent is EventCard next)
                sb.AppendLine($"Event this round: {EventCards.Title(now)} ({EventCards.Description(now)}) " +
                              $"Next round: {EventCards.Title(next)} ({EventCards.Description(next)}) Plan your trades for it.");
        }
        else if (game.Type == GameType.Classic)
            sb.AppendLine("Classic game: everyone plays on this one board and shares its companies.");
        else
            sb.AppendLine("Tournament game: this board and its companies are yours alone; opponents have their own copies.");
        sb.AppendLine();

        sb.AppendLine($"Map (x across, y down). Key: . empty, o circle, * star, {Company.LetterFor(0)}-{Company.LetterFor(game.Companies.Length - 1)} company squares, 1-5 squares offered to you.");
        AppendMap(sb, game);
        sb.AppendLine();

        sb.AppendLine("Companies:");
        bool any = false;
        foreach (var c in game.Companies.Where(c => c.Exists))
        {
            any = true;
            sb.Append($"  {c.Letter} = {c.Name}: price {currency.Format(c.Value)}, {game.Board.CompanySize(c.Index)} squares. Holdings:");
            for (int p = 0; p < game.Players.Count; p++)
            {
                if (game.Type == GameType.Tournament && p != me) continue;
                sb.Append($" {game.Players[p].Name} {game.Players[p].Shares[c.Index]:N0};");
            }
            sb.AppendLine();
        }
        if (!any) sb.AppendLine("  None yet.");
        sb.AppendLine();

        sb.AppendLine("Players:");
        for (int p = 0; p < game.Players.Count; p++)
        {
            var pl = game.Players[p];
            string you = p == me ? " (you)" : "";
            sb.AppendLine($"  {pl.Name}{you}: cash {currency.Format(pl.Money)}, net worth {currency.Format(game.NetWorth(p))}");
        }
        sb.AppendLine();

        sb.AppendLine("Your options:");
        for (int i = 0; i < game.Choices.Count; i++)
        {
            var square = game.Choices[i];
            sb.Append($"  {i + 1}. Square {square}: ");
            if (game.ValidateChoice(square) is string why)
            {
                sb.AppendLine($"NOT ALLOWED ({why})");
                continue;
            }
            var sim = game.Clone();
            var events = sim.PlaceSquare(square);
            sb.AppendLine(string.Join(" ", events.Select(e => EventText.Describe(sim, e, currency))));
            var prices = sim.Companies.Where(c => c.Exists).Select(c => $"{c.Letter} {currency.Format(c.Value)}");
            sb.AppendLine($"     Afterwards: your cash {currency.Format(sim.CurrentPlayer.Money)}; prices {string.Join(", ", prices)}.");
        }
        sb.AppendLine();
        sb.AppendLine("Pick your square and any trades.");
        return sb.ToString();
    }

    private static void AppendMap(StringBuilder sb, Game game)
    {
        var board = game.Board;
        var choiceIndex = game.Choices.Select((p, i) => (p, i)).ToDictionary(t => t.p, t => t.i + 1);

        sb.Append("     ");
        for (int x = 0; x < board.Width; x++) sb.Append((x % 10).ToString());
        sb.AppendLine();
        for (int y = 0; y < board.Height; y++)
        {
            sb.Append($"  {y,2} ");
            for (int x = 0; x < board.Width; x++)
            {
                var pos = new Pos(x, y);
                int cell = board[pos];
                char ch = choiceIndex.TryGetValue(pos, out int n) ? (char)('0' + n)
                    : cell == Board.Circle ? 'o'
                    : cell == Board.Diamond ? '*'
                    : cell >= Board.FirstCompany ? Company.LetterFor(Board.CompanyOf(cell))
                    : '.';
                sb.Append(ch);
            }
            sb.AppendLine();
        }
    }
}
