using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Core;

/// <summary>Preferences saved between sessions (replaces the old "Preferences" file).</summary>
public sealed record AppSettings
{
    /// <summary>Settings saved before version 1 used pounds by default; loading moves them to Galactic Bucks.</summary>
    public const int CurrentVersion = 1;

    /// <summary>0 when read from a file saved before versions were recorded.</summary>
    public int Version { get; init; }

    public string CurrencyUnit { get; init; } = Currency.GalacticBucks;
    public bool CurrencyPrefix { get; init; } = true;
    public GameRules Rules { get; init; } = new();
    public GridSize GridSize { get; init; } = GridSize.Small;

    /// <summary>The game type picked last in New Game. New players start with Modern.</summary>
    public GameType LastGameType { get; init; } = GameType.Modern;

    /// <summary>Open the Market window by itself after the player chooses a square (S16).</summary>
    public bool OpenMarketAfterSquare { get; init; }

    /// <summary>The Market window's last position and size (left, top, width, height), or null for the default.</summary>
    public int[]? MarketWindow { get; init; }

    /// <summary>Adaptive difficulty (S9): computer players move along a ladder with the human's results.</summary>
    public bool AdaptiveDifficulty { get; init; }

    /// <summary>The current step on the adaptive ladder (see <see cref="Ai.AdaptiveDifficulty.Steps"/>).</summary>
    public int AdaptiveStep { get; init; }

    /// <summary>The chaos level picked last for a Modern game.</summary>
    public Chaos LastChaos { get; init; } = Chaos.Standard;

    /// <summary>Pause between a computer player's actions so you can follow along.</summary>
    public int ComputerDelayMs { get; init; } = 800;

    /// <summary>Show pop-up dialogs for new companies, mergers and splits. Off: they appear in the news line and log (S8).</summary>
    public bool ShowComputerPopups { get; init; }

    /// <summary>The computer players' pause before "Fast" was switched on, to restore when it's switched off.</summary>
    public int? NormalComputerDelayMs { get; init; }

    public ClaudeCodeSettings Claude { get; init; } = new();

    [JsonIgnore]
    public Currency Currency => new(CurrencyUnit, CurrencyPrefix);
}

/// <param name="AdvisorUses">How many times the player asked the Advisor during the game (S14), so assisted scores are visible.</param>
/// <param name="Difficulty">With adaptive difficulty, the step the computer players were on (S9).</param>
public sealed record HighScoreEntry(string Name, BigInteger Wealth, PlayerKind Kind, GameType GameType, DateTime Date, int AdvisorUses = 0,
    string? Difficulty = null);

/// <summary>Top ten scores for each grid size (replaces the old encrypted "scores" file).</summary>
public sealed class HighScoreTable
{
    public const int MaxEntries = 10;

    public Dictionary<GridSize, List<HighScoreEntry>> Tables { get; set; } = new();

    /// <summary>Modern games score far lower than Classic ones, so they have tables of their own.</summary>
    public Dictionary<GridSize, List<HighScoreEntry>> ModernTables { get; set; } = new();

    public IReadOnlyList<HighScoreEntry> For(GridSize size, bool modern = false) =>
        (modern ? ModernTables : Tables).TryGetValue(size, out var list) ? list : [];

    /// <summary>Adds a score if it makes the table (Modern games go in the Modern tables). Returns its 0-based position, or -1.</summary>
    public int Add(GridSize size, HighScoreEntry entry)
    {
        var tables = entry.GameType == GameType.Modern ? ModernTables : Tables;
        if (!tables.TryGetValue(size, out var list))
            tables[size] = list = [];
        int index = list.FindIndex(e => entry.Wealth > e.Wealth);
        if (index < 0) index = list.Count;
        if (index >= MaxEntries) return -1;
        list.Insert(index, entry);
        if (list.Count > MaxEntries) list.RemoveRange(MaxEntries, list.Count - MaxEntries);
        return index;
    }

    /// <summary>The original game seeded the table with its author's name.</summary>
    public static HighScoreTable CreateDefault()
    {
        var table = new HighScoreTable();
        foreach (var size in Enum.GetValues<GridSize>())
            table.Tables[size] = Enumerable.Range(0, MaxEntries)
                .Select(_ => new HighScoreEntry("David", 1_000_000, PlayerKind.Human, GameType.Classic, new DateTime(2001, 10, 7)))
                .ToList();
        return table;
    }
}

/// <summary>Reads and writes JSON files under %LOCALAPPDATA%\SpaceMonopoly.</summary>
public sealed class Storage(string folder)
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(), new BigIntegerConverter() },
    };

    public static Storage Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceMonopoly"));

    public string Folder { get; } = folder;

    private string SettingsPath => Path.Combine(Folder, "settings.json");
    private string ScoresPath => Path.Combine(Folder, "highscores.json");

    public AppSettings LoadSettings()
    {
        var settings = Load<AppSettings>(SettingsPath);
        if (settings is null) return new AppSettings { Version = AppSettings.CurrentVersion };
        if (settings.Version < 1 && settings.CurrencyUnit == "£")
            settings = settings with { CurrencyUnit = Currency.GalacticBucks };
        return settings with { Version = AppSettings.CurrentVersion };
    }

    public void SaveSettings(AppSettings settings) => Save(SettingsPath, settings);

    public HighScoreTable LoadHighScores() => Load<HighScoreTable>(ScoresPath) ?? HighScoreTable.CreateDefault();

    public void SaveHighScores(HighScoreTable table) => Save(ScoresPath, table);

    private static T? Load<T>(string path) where T : class
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options) : null;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // A damaged file shouldn't stop the game from starting; defaults are used instead.
            return null;
        }
    }

    private void Save<T>(string path, T value)
    {
        Directory.CreateDirectory(Folder);
        string temp = path + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(value, Options));
        File.Move(temp, path, overwrite: true);
    }
}

/// <summary>Writes big numbers as plain JSON numbers, so files from earlier versions still load.</summary>
internal sealed class BigIntegerConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        string text = reader.TokenType == JsonTokenType.String
            ? reader.GetString() ?? "0"
            : System.Text.Encoding.UTF8.GetString(reader.HasValueSequence ? System.Buffers.BuffersExtensions.ToArray(reader.ValueSequence) : reader.ValueSpan.ToArray());
        return BigInteger.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
    }

    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options) =>
        writer.WriteRawValue(value.ToString(System.Globalization.CultureInfo.InvariantCulture));
}
