namespace SpaceMonopoly.Core;

/// <summary>
/// Classic and Tournament follow the 2001 rules. Modern is a shorter game on a shared board with a hand of squares,
/// limited share buying and majority bonuses (see <see cref="ModernRules"/>).
/// </summary>
public enum GameType { Classic, Tournament, Modern }

/// <summary>The numbers that make a Modern game different. Agreed with the user; see CLAUDE.md, S2–S4.</summary>
public static class ModernRules
{
    /// <summary>Rounds in a Modern game (fewer if they wouldn't fit on the board).</summary>
    public const int Rounds = 25;

    /// <summary>Squares each player holds.</summary>
    public const int HandSize = 4;

    /// <summary>Shares a player may buy in one turn, across all companies. Selling is unlimited.</summary>
    public const int BuyLimitPerTurn = 100;

    /// <summary>Shares in a new company, held by all players together.</summary>
    public const int SharesPerCompany = 1000;

    /// <summary>Free shares for founding a company (no cash bonus).</summary>
    public const long FreeShares = 3;

    /// <summary>At a merger the largest holder of the company taken over gets this many times its share price...</summary>
    public const long LargestHolderBonus = 10;

    /// <summary>...and the second-largest this many times.</summary>
    public const long SecondHolderBonus = 5;

    /// <summary>Only players holding at least this many shares of the company taken over qualify for a bonus (F3).</summary>
    public const int MinSharesForBonus = 10;

    /// <summary>Interest on cash, paid at the start of each player's turn (S6), rounded down.</summary>
    public const int InterestPercent = 3;

    /// <summary>A Modern board has about one planet in this many squares (Classic: one in 30).</summary>
    public const int PlanetOdds = 12;
}

public enum PlayerKind { Human, ComputerEasy, ComputerMedium, ComputerHard, Claude }

public static class PlayerKindExtensions
{
    public static bool IsComputer(this PlayerKind kind) => kind != PlayerKind.Human;

    public static string Label(this PlayerKind kind) => kind switch
    {
        PlayerKind.Human => "Human",
        PlayerKind.ComputerEasy => "Computer (Easy)",
        PlayerKind.ComputerMedium => "Computer (Medium)",
        PlayerKind.ComputerHard => "Computer (Hard)",
        PlayerKind.Claude => "Claude (via Claude Code)",
        _ => kind.ToString(),
    };
}

/// <summary>The optional multiplayer rules from the Preferences dialog.</summary>
public sealed record GameRules
{
    /// <summary>If a merger is on offer the player must take it.</summary>
    public bool ForceMergers { get; init; }

    /// <summary>If an expansion is on offer the player must take it.</summary>
    public bool ForceExpansion { get; init; }

    /// <summary>With ForceExpansion, founding a new company also satisfies the rule.</summary>
    public bool AllowCompanyCreation { get; init; }

    /// <summary>Hides other players' holdings during play.</summary>
    public bool HideDetails { get; init; }

    /// <summary>Modern only: how many event cards in the deck do nothing. Chosen in New Game.</summary>
    public Chaos Chaos { get; init; } = Chaos.Standard;
}

public sealed class Company
{
    /// <summary>The first five are the original companies; the rest were added for the bigger grids.</summary>
    public static readonly string[] Names =
    [
        "Atlantis Ltd.", "Betelguese", "Cannon Inc.", "Debenese", "Eridnus Corp.",
        "Fomalhaut Mining", "Ganymede Group", "Hyperion Corp.", "Io Industries", "Jovian Freight",
        "Kepler Ltd.", "Lyra Holdings",
    ];

    /// <summary>clRed, clLime, clYellow, clTeal and clBlue from CreateComs in Players.cpp, then new colours.</summary>
    public static readonly int[] Colors =
    [
        unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), unchecked((int)0xFFFFFF00), unchecked((int)0xFF008080), unchecked((int)0xFF0000FF),
        unchecked((int)0xFFFF8C00), unchecked((int)0xFF9400D3), unchecked((int)0xFFFF69B4), unchecked((int)0xFF8B4513), unchecked((int)0xFF00E5E5),
        unchecked((int)0xFF808000), unchecked((int)0xFFC8C8C8),
    ];

    /// <summary>The most companies any grid size allows.</summary>
    public const int MaxCount = 12;

    public Company(int index)
    {
        Index = index;
    }

    public int Index { get; }
    public string Name => Names[Index];
    public char Letter => LetterFor(Index);
    public int Argb => Colors[Index];

    public static char LetterFor(int index) => (char)('A' + index);

    /// <summary>True for light colours, where black text reads better than white.</summary>
    public static bool UsesDarkText(int index)
    {
        int argb = Colors[index];
        int r = (argb >> 16) & 0xFF, g = (argb >> 8) & 0xFF, b = argb & 0xFF;
        return 0.299 * r + 0.587 * g + 0.114 * b > 140;
    }

    /// <summary>Price of one share.</summary>
    public long Value { get; internal set; }

    public bool Exists { get; internal set; }

    /// <summary>Modern only: how many shares exist, held by all players together. Zero means unlimited.</summary>
    public BigInteger Supply { get; internal set; }

    internal Company Clone() => new(Index) { Value = Value, Exists = Exists, Supply = Supply };

    public static Company[] CreateSet(int count) => Enumerable.Range(0, count).Select(i => new Company(i)).ToArray();
}

public sealed class Player
{
    public Player(string name, PlayerKind kind, BigInteger money, int companies)
    {
        Name = name;
        Kind = kind;
        Money = money;
        Shares = new BigInteger[companies];
    }

    public string Name { get; }
    public PlayerKind Kind { get; }
    /// <summary>Money and share counts can grow past the range of a 64-bit number in long games.</summary>
    public BigInteger Money { get; internal set; }
    public BigInteger[] Shares { get; private init; }

    public bool IsHuman => Kind == PlayerKind.Human;

    internal Player Clone() => new(Name, Kind, Money, 0) { Shares = (BigInteger[])Shares.Clone() };
}

/// <summary>A board plus its companies. Classic games share one world; tournament games give each player their own.</summary>
public sealed class World
{
    public World(Board board, Company[] companies)
    {
        Board = board;
        Companies = companies;
    }

    public Board Board { get; }
    public Company[] Companies { get; }

    internal World Clone() => new(Board.Clone(), Companies.Select(c => c.Clone()).ToArray());
}

public sealed record Standing(int Player, string Name, PlayerKind Kind, BigInteger Money, BigInteger ShareWorth)
{
    public BigInteger NetWorth => Money + ShareWorth;
}
