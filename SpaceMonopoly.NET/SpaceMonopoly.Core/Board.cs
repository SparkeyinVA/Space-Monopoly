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
/// 2 = diamond (star), 3 and up = company 0, 1, 2...
/// </summary>
public sealed class Board
{
    public const int Empty = 0;
    public const int Circle = 1;
    public const int Diamond = 2;
    public const int FirstCompany = 3;

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
    }

    public int Width { get; }
    public int Height { get; }

    public int this[int x, int y]
    {
        get => _cells[y * Width + x];
        set => _cells[y * Width + x] = value;
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

    /// <summary>The up/left/right/down neighbours that are on the board.</summary>
    public IEnumerable<Pos> Neighbours(Pos p)
    {
        if (p.Y > 0) yield return p with { Y = p.Y - 1 };
        if (p.X > 0) yield return p with { X = p.X - 1 };
        if (p.X < Width - 1) yield return p with { X = p.X + 1 };
        if (p.Y < Height - 1) yield return p with { Y = p.Y + 1 };
    }

    public void Randomise(Random rng)
    {
        for (int i = 0; i < _cells.Length; i++)
            _cells[i] = rng.Next(CircleOdds) == 1 ? Circle : Empty;
    }

    /// <summary>Removes diamonds and company squares but keeps the circles.</summary>
    public void ClearPieces()
    {
        for (int i = 0; i < _cells.Length; i++)
            if (_cells[i] != Circle) _cells[i] = Empty;
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
        bool Open(Pos p) => this[p] == Empty && Survey(p).Companies.Count == 0;

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
        var companies = new SortedSet<int>();
        foreach (var n in Neighbours(p))
        {
            int cell = this[n];
            if (cell == Circle) circles++;
            else if (cell == Diamond) diamonds++;
            else if (cell >= FirstCompany) companies.Add(CompanyOf(cell));
        }
        return new Surroundings(circles, diamonds, companies.ToArray());
    }
}

/// <summary>Neighbourhood of a square (comAdjoining in SMMain.cpp). Companies are in index order.</summary>
public sealed record Surroundings(int Circles, int Diamonds, IReadOnlyList<int> Companies)
{
    public bool NextToCircleOrDiamond => Circles > 0 || Diamonds > 0;
}
