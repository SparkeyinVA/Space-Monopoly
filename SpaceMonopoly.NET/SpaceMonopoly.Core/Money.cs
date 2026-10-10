using System.Globalization;

namespace SpaceMonopoly.Core;

/// <summary>Formats amounts with thousands separators and the chosen currency unit (CurrencyUnit.cpp).</summary>
public sealed record Currency(string Unit = Currency.GalacticBucks, bool Prefix = true)
{
    /// <summary>The game's own made-up currency (shown as ₲1,250).</summary>
    public const string GalacticBucks = "₲";

    public static readonly Currency Default = new();

    /// <summary>Amounts below this are shown in full; bigger ones as "4.25 quintillion".</summary>
    private static readonly BigInteger ShortFormFrom = BigInteger.Pow(10, 15);

    private static readonly string[] BigNames =
    [
        "quadrillion", "quintillion", "sextillion", "septillion", "octillion",
        "nonillion", "decillion", "undecillion", "duodecillion", "tredecillion",
    ];

    public string Format(BigInteger amount)
    {
        string number = Number(amount);
        return Prefix ? Unit + number : number + " " + Unit;
    }

    /// <summary>
    /// The amount with thousands separators. Long games can produce enormous fortunes, so from a
    /// quadrillion upwards the amount is shortened, e.g. "4.25 quintillion".
    /// </summary>
    public static string Number(BigInteger amount)
    {
        if (BigInteger.Abs(amount) < ShortFormFrom)
            return amount.ToString("N0", CultureInfo.InvariantCulture);

        int digits = BigInteger.Abs(amount).ToString(CultureInfo.InvariantCulture).Length;
        int group = (digits - 1) / 3;              // 5 = quadrillion (10^15), 6 = quintillion...
        int nameIndex = group - 5;
        if (nameIndex >= BigNames.Length)
            return ((double)amount).ToString("0.00e0", CultureInfo.InvariantCulture);

        double scaled = (double)amount / Math.Pow(10, group * 3);
        return scaled.ToString("0.##", CultureInfo.InvariantCulture) + " " + BigNames[nameIndex];
    }
}
