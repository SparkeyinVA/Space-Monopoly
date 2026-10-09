using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

public class StorageTests : IDisposable
{
    private readonly string _folder = Path.Combine(Path.GetTempPath(), "SpaceMonopolyTests-" + Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_folder)) Directory.Delete(_folder, recursive: true);
    }

    [Fact]
    public void High_scores_keep_the_top_ten_in_order()
    {
        var table = HighScoreTable.CreateDefault();
        var entry = new HighScoreEntry("Jerame", 2_500_000, PlayerKind.Human, GameType.Classic, DateTime.Now);

        Assert.Equal(0, table.Add(GridSize.Small, entry));
        Assert.Equal(-1, table.Add(GridSize.Small, entry with { Wealth = 5 }));
        Assert.Equal(10, table.For(GridSize.Small).Count);
        Assert.Equal("Jerame", table.For(GridSize.Small)[0].Name);
    }

    [Fact]
    public void Settings_and_scores_round_trip()
    {
        var storage = new Storage(_folder);
        var settings = new AppSettings { CurrencyUnit = "$", Rules = new GameRules { ForceMergers = true }, GridSize = GridSize.Large };
        storage.SaveSettings(settings);
        var table = HighScoreTable.CreateDefault();
        table.Add(GridSize.Medium, new HighScoreEntry("Claude", 9_000_000, PlayerKind.Claude, GameType.Tournament, DateTime.Today));
        storage.SaveHighScores(table);

        var loaded = storage.LoadSettings();
        Assert.Equal("$", loaded.CurrencyUnit);
        Assert.True(loaded.Rules.ForceMergers);
        Assert.Equal(GridSize.Large, loaded.GridSize);
        Assert.Equal("Claude", storage.LoadHighScores().For(GridSize.Medium)[0].Name);
    }

    [Fact]
    public void Damaged_files_fall_back_to_defaults()
    {
        Directory.CreateDirectory(_folder);
        File.WriteAllText(Path.Combine(_folder, "settings.json"), "{ not json");
        Assert.Equal("£", new Storage(_folder).LoadSettings().CurrencyUnit);
    }

    [Fact]
    public void Currency_formats_with_separators()
    {
        Assert.Equal("£1,234,567", Currency.Default.Format(1_234_567));
        Assert.Equal("1,000 EUR", new Currency("EUR", Prefix: false).Format(1000));
    }
}
