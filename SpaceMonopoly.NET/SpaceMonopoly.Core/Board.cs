namespace SpaceMonopoly.Core;

/// <summary>A square on the board.</summary>
public readonly record struct Pos(int X, int Y)
{
    public override string ToString() => $"({X},{Y})";
}

public enum GridSize { Small, Medium, Large }

/// <summary>
/// Board dimensions, goes per player and company limit for each grid size. Sizes and goes come from
/// setGridSize in SMMain.cpp, where the goes were shared by all players; here every player gets them.
/// </summary>
public sealed record GridSpec(GridSize Size, int Width, int Height, int GoesPerPlayer, int Companies)
{
    public static GridSpec For(GridSize size) => size switch
    {
        GridSize.Small => new(size, 16, 12, 80, 5),
        GridSize.Medium => new(size, 22, 18, 120, 8),
        GridSize.Large => new(size, 28, 24, 160, 12),
        _ => throw new ArgumentOutOfRangeException(nameof(size)),
    };

    public string Label => $"{Size} ({Width} x {Height})";
}

/// <summary>
/// The playing grid. Cell values follow the original game: 0 = empty, 1 = circle (planet),
/// 2 = diamond (star), 3 and up = company 0, 1, 2... Modern's special squares (S11) are negative:
/// a nebula, a black hole and the two ends of a wormhole. None of them can be played.
/// </summary>
public sealed class Board
{
    public const int Empty = 0;
    public const int Circle = 1;
    public const int Diamond = 2;
    public const int FirstCompany = 3;

    /// <summary>S11: a company square next to a nebula gets double the planet bonus.</summary>
    public const int Nebula = -1;

    /// <summary>S11: squares next to a black hole can't be played.</summary>
    public const int BlackHole = -2;

    /// <summary>S11: the two ends of a wormhole count as next to each other.</summary>
    public const int Wormhole = -3;

    public static bool IsSpecial(int cell) => cell < 0;

    /// <summary>Chance of any square being a circle is 1 in this (GameGrid::randomise).</summary>
    public const int CircleOdds = 30;

    private readonly int[] _cells;

    public Board(int width, int height)
    {
        if (width <= 0 || height <= 0) throw new ArgumentOutOfRangeException(nameof(width));
        Width = width;
        Height = height;
        _cells = new int[width * height];
    }

    private Board(Board other)
    {
        Width = other.Width;
        Height = other.Height;
        _cells = (int[])other._cells.Clone();
        _wormholes = other._wormholes;
    }

    // The wormhole ends, found when first needed and forgotten whenever one is placed or removed.
    private Pos[]? _wormholes;

    private Pos[] Wormholes => _wormholes ??= AllPositions().Where(p => this[p] == Wormhole).ToArray();

    public int Width { get; }
    public int Height { get; }

    public int this[int x, int y]
    {
        get => _cells[y * Width + x];
        set
        {
            ref int cell = ref _cells[y * Width + x];
            if (cell == Wormhole || value == Wormhole) _wormholes = null;
            cell = value;
        }
    }

    public int this[Pos p]
    {
        get => this[p.X, p.Y];
        set => this[p.X, p.Y] = value;
    }

    public static int CellForCompany(int company) => company + FirstCompany;

    /// <summary>Returns the company index occupying a cell value, or -1.</summary>
    public static int CompanyOf(int cell) => cell >= FirstCompany ? cell - FirstCompany : -1;

    public Board Clone() => new(this);

    public bool InBounds(Pos p) => p.X >= 0 && p.Y >= 0 && p.X < Width && p.Y < Height;

    public IEnumerable<Pos> AllPositions()
    {
        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                yield return new Pos(x, y);
    }

    /// <summary>
    /// The up/left/right/down neighbours that are on the board. Next to a wormhole end, the squares around the
    /// other end count as neighbours too (S11), so companies can grow and merge across the board.
    /// </summary>
    public IEnumerable<Pos> Neighbours(Pos p)
    {
        foreach (var n in Adjacent(p)) yield return n;
        var ends = Wormholes;
        if (ends.Length != 2) yield break;
        for (int i = 0; i < 2; i++)
        {
            if (!Adjacent(p).Contains(ends[i])) continue;
            foreach (var n in Adjacent(ends[1 - i]))
                if (n != p) yield return n;
        }
    }

    private IEnumerable<Pos> Adjacent(Pos p)
    {
        if (p.Y > 0) yield return p with { Y = p.Y - 1 };
        if (p.X > 0) yield return p with { X = p.X - 1 };
        if (p.X < Width - 1) yield return p with { X = p.X + 1 };
        if (p.Y < Height - 1) yield return p with { Y = p.Y + 1 };
    }

    /// <summary>
    /// S11: places one nebula, one black hole and a wormhole pair on empty squares. The nebula goes next to a planet
    /// where possible (so it matters), and the wormhole ends at least half the board apart.
    /// </summary>
    public void PlaceSpecials(Random rng)
    {
        List<Pos> Free() => AllPositions().Where(p => this[p] == Empty && Adjacent(p).All(n => !IsSpecial(this[n]))).ToList();
        Pos Pick(List<Pos> from) => from[rng.Next(from.Count)];

        var free = Free();
        var nearPlanet = free.Where(p => Adjacent(p).Any(n => this[n] == Circle)).ToList();
        if (free.Count == 0) return;
        this[Pick(nearPlanet.Count > 0 ? nearPlanet : free)] = Nebula;
        if ((free = Free()).Count == 0) return;
        this[Pick(free)] = BlackHole;
        if ((free = Free()).Count < 2) return;
        var a = Pick(free);
        var far = free.Where(p => Math.Abs(p.X - a.X) + Math.Abs(p.Y - a.Y) >= (Width + Height) / 2).ToList();
        if (far.Count == 0) return;
        this[a] = Wormhole;
        this[Pick(far)] = Wormhole;
    }

    /// <summary>Fills the board with planets, about one in <paramref name="odds"/> squares.</summary>
    public void Randomise(Random rng, int odds = CircleOdds)
    {
        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = rng.Next(odds) == 1 ? Circle : Empty;   // == 1, as in the original, so old seeds give the same boards
    }

    /// <summary>Removes diamonds and company squares but keeps the circles and special squares.</summary>
    public void ClearPieces()
    {
        for (int i = 0; i < _cells.Length; i++)
            if (_cells[i] != Circle && !IsSpecial(_cells[i])) _cells[i] = Empty;
    }

    public int CountOf(int cellValue)
    {
        int n = 0;
        foreach (int c in _cells)
            if (c == cellValue) n++;
        return n;
    }

    public int CompanySize(int company) => CountOf(CellForCompany(company));

    /// <summary>Replaces every occurrence of one cell value with another (Spread in SMMain.cpp).</summary>
    public void Replace(int from, int to)
    {
        for (int i = 0; i < _cells.Length; i++)
            if (_cells[i] == from) _cells[i] = to;
    }

    /// <summary>
    /// True while a new company could still be founded somewhere, now or later. A company needs an empty
    /// square with no company next to it that touches a planet or star. A star can only be placed on such a
    /// square too, so a square with no company beside it next to another such square can still become one.
    /// Company squares never go back to empty, so once this is false it stays false.
    /// </summary>
    public bool CanStillFoundCompany()
    {
        bool Open(Pos p) => this[p] == Empty && Survey(p) is { Companies.Count: 0, NearBlackHole: false };

        foreach (var p in AllPositions())
        {
            if (!Open(p)) continue;
            foreach (var n in Neighbours(p))
                if (this[n] is Circle or Diamond || Open(n)) return true;
        }
        return false;
    }

    /// <summary>What surrounds a square: counts of circles and diamonds plus the adjoining companies.</summary>
    public Surroundings Survey(Pos p)
    {
        int circles = 0, diamonds = 0;
        bool nebula = false, blackHole = false;
        var companies = new SortedSet<int>();
        foreach (var n in Neighbours(p))
        {
            int cell = this[n];
            if (cell == Circle) circles++;
            else if (cell == Diamond) diamonds++;
            else if (cell >= FirstCompany) companies.Add(CompanyOf(cell));
            else if (cell == Nebula) nebula = true;
            else if (cell == BlackHole) blackHole = true;
        }
        return new Surroundings(circles, diamonds, companies.ToArray(), nebula, blackHole);
    }
}

/// <summary>Neighbourhood of a square (comAdjoining in SMMain.cpp). Companies are in index order.</summary>
/// <param name="NearNebula">S11: the square touches a nebula, so its planet bonus is doubled.</param>
/// <param name="NearBlackHole">S11: the square touches a black hole, so it can't be played.</param>
public sealed record Surroundings(int Circles, int Diamonds, IReadOnlyList<int> Companies, bool NearNebula = false, bool NearBlackHole = false)
{
    public bool NextToCircleOrDiamond => Circles > 0 || Diamonds > 0;
}
