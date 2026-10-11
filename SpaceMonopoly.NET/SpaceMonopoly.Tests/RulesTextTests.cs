using SpaceMonopoly.Core;

namespace SpaceMonopoly.Tests;

/// <summary>How to Play shows only the rules of the game type being played (F10).</summary>
public class RulesTextTests
{
    private static readonly string[] ClassicOnly =
        ["free shares plus cash", "cash dividend", "AND get the same number of shares", "SHARE SPLITS"];

    private static readonly string[] ModernOnly =
        ["your hand", "interest", "Prospect", "secret goal", "convert", "buying allowance"];

    [Theory]
    [InlineData(GameType.Classic)]
    [InlineData(GameType.Tournament)]
    public void The_2001_rules_have_no_Modern_text(GameType type)
    {
        string text = RulesText.For(type);
        foreach (var phrase in ClassicOnly) Assert.Contains(phrase, text);
        foreach (var phrase in ModernOnly) Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Modern_rules_have_no_Classic_text_and_explain_mergers_with_an_example()
    {
        string text = RulesText.For(GameType.Modern);
        foreach (var phrase in ClassicOnly) Assert.DoesNotContain(phrase, text, StringComparison.OrdinalIgnoreCase);
        foreach (var phrase in ModernOnly) Assert.Contains(phrase, text, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Example: Atlantis", text);
        Assert.Contains($"{ModernRules.InterestPercent}% interest", text);
    }
}
