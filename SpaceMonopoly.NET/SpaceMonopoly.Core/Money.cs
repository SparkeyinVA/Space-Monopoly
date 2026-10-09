using System.Globalization;

namespace SpaceMonopoly.Core;

/// <summary>Formats amounts with thousands separators and the chosen currency unit (CurrencyUnit.cpp).</summary>
public sealed record Currency(string Unit = "£", bool Prefix = true)
{
    public static readonly Currency Default = new();

    public string Format(long amount)
    {
        string number = amount.ToString("N0", CultureInfo.InvariantCulture);
        return Prefix ? Unit + number : number + " " + Unit;
    }
}
