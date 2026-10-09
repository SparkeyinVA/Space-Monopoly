using System.Text.Json;
using System.Text.Json.Serialization;
using SpaceMonopoly.Core.Ai;

namespace SpaceMonopoly.Core;

/// <summary>Preferences saved between sessions (replaces the old "Preferences" file).</summary>
public sealed record AppSettings
{
    public string CurrencyUnit { get; init; } = "£";
    public bool CurrencyPrefix { get; init; } = true;
    public GameRules Rules { get; init; } = new();
    public GridSize GridSize { get; init; } = GridSize.Small;

    /// <summary>Pause between a computer player's actions so you can follow along.</summary>
    public int ComputerDelayMs { get; init; } = 800;

    /// <summary>Show pop-up dialogs for computer players' new companies, mergers and splits.</summary>
    public bool ShowComputerPopups { get; init; }

    public ClaudeCodeSettings Claude { get; init; } = new();

    [JsonIgnore]
    public Currency Currency => new(CurrencyUnit, CurrencyPrefix);
}

public sealed record HighScoreEntry(string Name, long Wealth, PlayerKind Kind, GameType GameType, DateTime Date);

/// <summary>Top ten scores for each grid size (replaces the old encrypted "scores" file).</summary>
public sealed class HighScoreTable
{
    public const int MaxEntries = 10;

    public Dictionary<GridSize, List<HighScoreEntry>> Tables { get; set; } = new();

    public IReadOnlyList<HighScoreEntry> For(GridSize size) =>
        Tables.TryGetValue(size, out var list) ? list : [];

    /// <summary>Adds a score if it makes the table. Returns its 0-based position, or -1.</summary>
    public int Add(GridSize size, HighScoreEntry entry)
    {
        if (!Tables.TryGetValue(size, out var list))
            Tables[size] = list = [];
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
        Converters = { new JsonStringEnumConverter() },
    };

    public static Storage Default { get; } = new(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpaceMonopoly"));

    public string Folder { get; } = folder;

    private string SettingsPath => Path.Combine(Folder, "settings.json");
    private string ScoresPath => Path.Combine(Folder, "highscores.json");

    public AppSettings LoadSettings() => Load<AppSettings>(SettingsPath) ?? new AppSettings();

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
