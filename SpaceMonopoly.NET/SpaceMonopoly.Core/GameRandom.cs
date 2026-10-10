namespace SpaceMonopoly.Core;

/// <summary>
/// The game's random numbers (SplitMix64). Unlike <see cref="Random"/>, a copy can be made without using up
/// any of the original's numbers, so previews and computer look-ahead on <see cref="Game.Clone"/> never change
/// which squares or cards the real game deals next.
/// </summary>
internal sealed class GameRandom
{
    private ulong _state;

    public GameRandom(long seed) => _state = (ulong)seed;

    public static GameRandom FromSeed(int? seed) => new(seed ?? Random.Shared.NextInt64());

    /// <summary>
    /// A generator for a copy of the game. It follows a different sequence from this one, so a look-ahead
    /// can't see what the real game will deal, and making it leaves this one untouched.
    /// </summary>
    public GameRandom Fork() => new((long)Mix(_state ^ 0xD1B54A32D192ED03UL));

    /// <summary>A number from 0 up to, but not including, <paramref name="max"/>.</summary>
    public int Next(int max)
    {
        if (max <= 0) throw new ArgumentOutOfRangeException(nameof(max));
        return (int)(NextUInt64() % (ulong)max);
    }

    /// <summary>A number from <paramref name="min"/> up to, but not including, <paramref name="max"/>.</summary>
    public int Next(int min, int max) => min + Next(max - min);

    private ulong NextUInt64()
    {
        _state += 0x9E3779B97F4A7C15UL;
        return Mix(_state);
    }

    private static ulong Mix(ulong z)
    {
        z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
        z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
        return z ^ (z >> 31);
    }
}
