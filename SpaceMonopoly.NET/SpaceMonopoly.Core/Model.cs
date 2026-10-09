namespace SpaceMonopoly.Core;

public enum GameType { Classic, Tournament }

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
}

public sealed class Company
{
    public static readonly string[] Names = ["Atlantis Ltd.", "Betelguese", "Cannon Inc.", "Debenese", "Eridnus Corp."];

    /// <summary>clRed, clLime, clYellow, clTeal, clBlue from CreateComs in Players.cpp.</summary>
    public static readonly int[] Colors = [unchecked((int)0xFFFF0000), unchecked((int)0xFF00FF00), unchecked((int)0xFFFFFF00), unchecked((int)0xFF008080), unchecked((int)0xFF0000FF)];

    public const int Count = 5;

    public Company(int index)
    {
        Index = index;
    }

    public int Index { get; }
    public string Name => Names[Index];
    public char Letter => (char)('A' + Index);
    public int Argb => Colors[Index];

    /// <summary>Price of one share.</summary>
    public long Value { get; internal set; }

    public bool Exists { get; internal set; }

    internal Company Clone() => new(Index) { Value = Value, Exists = Exists };

    public static Company[] CreateSet() => Enumerable.Range(0, Count).Select(i => new Company(i)).ToArray();
}

public sealed class Player
{
    public Player(string name, PlayerKind kind, long money)
    {
        Name = name;
        Kind = kind;
        Money = money;
    }

    public string Name { get; }
    public PlayerKind Kind { get; }
    public long Money { get; internal set; }
    public long[] Shares { get; private init; } = new long[Company.Count];

    public bool IsHuman => Kind == PlayerKind.Human;

    internal Player Clone() => new(Name, Kind, Money) { Shares = (long[])Shares.Clone() };
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

public sealed record Standing(int Player, string Name, PlayerKind Kind, long Money, long ShareWorth)
{
    public long NetWorth => Money + ShareWorth;
}
