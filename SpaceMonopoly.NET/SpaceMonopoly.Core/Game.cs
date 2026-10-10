namespace SpaceMonopoly.Core;

public enum GamePhase { ChoosingSquare, Trading, Over }

/// <summary>
/// The rules engine, ported from SMMain.cpp, ComForm.cpp, Merger.cpp and SplitForm.cpp.
/// It has no UI so the computer players can simulate moves on a clone.
/// </summary>
/// <remarks>
/// Deliberate fixes to bugs in the 2001 code:
/// <list type="bullet">
/// <item>Shares in an absorbed company are zeroed after a merger (they used to keep counting toward net worth).</item>
/// <item>Tournament mergers now move the absorbed shares into the survivor (the old loop never ran).</item>
/// <item>Splits repeat while the price is still 3,200 or more.</item>
/// <item>Square selection can't hang when fewer than five legal squares are left.</item>
/// </list>
/// </remarks>
public sealed class Game
{
    public const long StartingMoney = 6000;
    public const long SplitThreshold = 3200;
    public const long FreeShares = 5;
    public const int ChoiceCount = 5;
    public const long SquareValue = 100;
    public const long CircleBonus = 500;
    public const long DiamondBonus = 100;

    /// <summary>
    /// When every player's goes wouldn't fit on the board, goes are reduced so that about this share of
    /// the empty squares gets used. Room for new companies usually runs out when a board is 75-80% full, which
    /// ends the game early (see <see cref="Rounds"/>), so a higher share would promise rounds that never get played.
    /// </summary>
    public const double BoardFillLimit = 0.75;

    private readonly World[] _worlds;
    private readonly Player[] _players;
    private readonly Random _rng;
    private List<Pos> _choices = [];

    public Game(GridSpec spec, Board startingBoard, IEnumerable<(string Name, PlayerKind Kind)> players,
        GameType type, GameRules rules, int? seed = null)
    {
        _players = players.Select(p => new Player(p.Name, p.Kind, StartingMoney, spec.Companies)).ToArray();
        if (_players.Length == 0) throw new ArgumentException("A game needs at least one player.", nameof(players));
        if (startingBoard.Width != spec.Width || startingBoard.Height != spec.Height)
            throw new ArgumentException("Board size doesn't match the grid size.", nameof(startingBoard));

        Spec = spec;
        // Tournament only makes sense with two or more players (Players.cpp).
        Type = _players.Length == 1 ? GameType.Classic : type;
        Rules = rules;
        _rng = seed is int s ? new Random(s) : new Random();

        var board = startingBoard.Clone();
        board.ClearPieces();
        _worlds = Type == GameType.Classic
            ? [new World(board, Company.CreateSet(spec.Companies))]
            : _players.Select(_ => new World(board.Clone(), Company.CreateSet(spec.Companies))).ToArray();

        GoesPerPlayer = GoesThatFit(spec, board.CountOf(Board.Empty), Type == GameType.Classic ? _players.Length : 1);

        BeginTurn();
    }

    private Game(Game other)
    {
        Spec = other.Spec;
        Type = other.Type;
        Rules = other.Rules;
        _players = other._players.Select(p => p.Clone()).ToArray();
        _worlds = other._worlds.Select(w => w.Clone()).ToArray();
        _choices = [.. other._choices];
        _rng = new Random(other._rng.Next());
        CurrentPlayerIndex = other.CurrentPlayerIndex;
        Turn = other.Turn;
        GoesPerPlayer = other.GoesPerPlayer;
        _finalRound = other._finalRound;
        Phase = other.Phase;
    }

    public GridSpec Spec { get; }
    public GameType Type { get; }
    public GameRules Rules { get; }
    public IReadOnlyList<Player> Players => _players;
    public int CurrentPlayerIndex { get; private set; }
    public Player CurrentPlayer => _players[CurrentPlayerIndex];

    /// <summary>Zero-based round counter. Each player has one go per round.</summary>
    public int Turn { get; private set; }

    /// <summary>
    /// How many goes each player gets: the grid size's number, or fewer if that many wouldn't fit on the board.
    /// </summary>
    public int GoesPerPlayer { get; }

    private int? _finalRound;

    /// <summary>
    /// How many rounds the game will last: <see cref="GoesPerPlayer"/>, or fewer once no more companies can be
    /// founded. In a tournament this happens as soon as it happens on any player's board.
    /// </summary>
    public int Rounds => _finalRound is int last ? Math.Min(GoesPerPlayer, last + 1) : GoesPerPlayer;

    /// <summary>True once no new company can ever be founded and the game is playing out its last rounds.</summary>
    public bool NoMoreCompanies => _finalRound is not null;

    /// <summary>True when the board was too small for the grid size's full number of goes.</summary>
    public bool GoesReduced => GoesPerPlayer < Spec.GoesPerPlayer;

    /// <summary>Goes per player that fit on a board with this many empty squares shared by this many players.</summary>
    public static int GoesThatFit(GridSpec spec, int emptySquares, int playersSharingBoard)
    {
        int fit = (int)(emptySquares * BoardFillLimit) / Math.Max(1, playersSharingBoard);
        return Math.Clamp(fit, 1, spec.GoesPerPlayer);
    }

    public GamePhase Phase { get; private set; }

    /// <summary>The squares on offer this turn (the flashing blue squares).</summary>
    public IReadOnlyList<Pos> Choices => _choices;

    public World CurrentWorld => WorldFor(CurrentPlayerIndex);
    public Board Board => CurrentWorld.Board;
    public Company[] Companies => CurrentWorld.Companies;

    public World WorldFor(int player) => Type == GameType.Classic ? _worlds[0] : _worlds[player];

    /// <summary>Moves left in the game that can affect the current world, including this one.</summary>
    public int RemainingGoes
    {
        get
        {
            int rounds = Math.Max(0, Rounds - Turn);
            if (Type == GameType.Tournament) return rounds;
            return Math.Max(0, rounds * _players.Length - CurrentPlayerIndex);
        }
    }

    public Game Clone() => new(this);

    // ------------------------------------------------------------------ turns

    private void BeginTurn()
    {
        _choices = GenerateChoices();
        Phase = _choices.Count == 0 ? GamePhase.Over : GamePhase.ChoosingSquare;
    }

    /// <summary>Picks up to five empty squares to offer (go() in SMMain.cpp).</summary>
    private List<Pos> GenerateChoices()
    {
        var board = Board;
        bool canFound = Companies.Any(c => !c.Exists);
        var candidates = new List<Pos>();
        foreach (var p in board.AllPositions())
        {
            if (board[p] != Board.Empty) continue;
            if (!canFound)
            {
                // With every company in play, a square that would found a company can't be offered.
                var s = board.Survey(p);
                if (s.NextToCircleOrDiamond && s.Companies.Count == 0) continue;
            }
            candidates.Add(p);
        }

        for (int i = 0; i < Math.Min(ChoiceCount, candidates.Count); i++)
        {
            int j = _rng.Next(i, candidates.Count);
            (candidates[i], candidates[j]) = (candidates[j], candidates[i]);
        }
        return candidates.Take(ChoiceCount).ToList();
    }

    /// <summary>Ends the current player's turn and starts the next. Returns true when the game is over.</summary>
    public bool EndTurn()
    {
        if (Phase == GamePhase.Over) return true;
        if (Phase != GamePhase.Trading) throw new InvalidOperationException("Choose a square before ending the turn.");

        CurrentPlayerIndex = (CurrentPlayerIndex + 1) % _players.Length;
        if (CurrentPlayerIndex == 0) Turn++;

        if (Turn >= Rounds)
        {
            Phase = GamePhase.Over;
            _choices = [];
            return true;
        }

        BeginTurn();
        return Phase == GamePhase.Over;
    }

    /// <summary>Lets tests offer specific squares.</summary>
    internal void SetChoicesForTesting(params Pos[] choices)
    {
        _choices = [.. choices];
        Phase = GamePhase.ChoosingSquare;
    }

    /// <summary>Ends the game early (Game > End Game).</summary>
    public void EndNow()
    {
        Phase = GamePhase.Over;
        _choices = [];
    }

    // ------------------------------------------------------------------ choosing a square

    /// <summary>
    /// Checks a square against the offered choices and the forced-move preferences
    /// (Image1MouseDown in SMMain.cpp). Returns an explanation, or null if the move is allowed.
    /// </summary>
    public string? ValidateChoice(Pos p)
    {
        if (Phase != GamePhase.ChoosingSquare) return "It isn't time to choose a square.";
        if (!_choices.Contains(p)) return "Choose one of the highlighted squares.";
        if (!Rules.ForceMergers && !Rules.ForceExpansion) return null;

        var board = Board;
        var chosen = board.Survey(p);
        var all = _choices.Select(board.Survey).ToList();

        bool mergerOnOffer = Rules.ForceMergers && all.Any(s => s.Companies.Count > 1);
        if (mergerOnOffer && chosen.Companies.Count < 2)
            return "You must choose a square that causes a merger.";

        bool expansionOnOffer = Rules.ForceExpansion && all.Any(s => s.Companies.Count > 0);
        if (expansionOnOffer)
        {
            bool fails = Rules.AllowCompanyCreation
                ? chosen.Circles == 0 && chosen.Diamonds == 0 && chosen.Companies.Count == 0
                : chosen.Companies.Count == 0;
            if (fails)
                return Rules.AllowCompanyCreation
                    ? "You must choose a square that expands a company (or creates a new one)."
                    : "You must choose a square that expands a company.";
        }
        return null;
    }

    public IReadOnlyList<Pos> LegalChoices() => _choices.Where(p => ValidateChoice(p) is null).ToList();

    /// <summary>Plays the chosen square and returns what happened. Trading is allowed afterwards.</summary>
    public IReadOnlyList<GameEvent> PlaceSquare(Pos p)
    {
        if (ValidateChoice(p) is string error) throw new InvalidOperationException(error);

        var events = new List<GameEvent>();
        _choices = [];
        Act(p, events);
        Phase = GamePhase.Trading;

        // Once no company can be founded, the game ends after this round and one more.
        if (_finalRound is null && !Board.CanStillFoundCompany())
        {
            _finalRound = Turn + 1;
            events.Add(new NoMoreCompaniesEvent(CurrentPlayerIndex, Rounds));
        }
        return events;
    }

    /// <summary>Shareholders affected by changes in the current world.</summary>
    private IEnumerable<int> Recipients =>
        Type == GameType.Classic ? Enumerable.Range(0, _players.Length) : [CurrentPlayerIndex];

    private void Act(Pos p, List<GameEvent> events)
    {
        var board = Board;
        var around = board.Survey(p);

        switch (around.Companies.Count)
        {
            case 0:
                if (around.NextToCircleOrDiamond && Companies.Any(c => !c.Exists))
                {
                    Found(p, events);
                }
                else
                {
                    board[p] = Board.Diamond;
                    events.Add(new DiamondPlacedEvent(CurrentPlayerIndex, p));
                }
                break;

            case 1:
                Expand(p, around.Companies[0], events);
                break;

            default:
                // The bigger company takes over; on a tie the lower-numbered one survives.
                int survivor = around.Companies[0], absorbed = around.Companies[1];
                if (board.CompanySize(absorbed) > board.CompanySize(survivor))
                    (survivor, absorbed) = (absorbed, survivor);

                Merge(survivor, absorbed, events);
                board[p] = Board.CellForCompany(survivor);
                board.Replace(Board.CellForCompany(absorbed), Board.CellForCompany(survivor));
                Companies[absorbed].Exists = false;
                Companies[absorbed].Value = 0;

                // Check for a further merger, then the square itself expands the survivor.
                Act(p, events);
                break;
        }
    }

    /// <summary>Claims the square for a company and returns its value (UpdateGrid in SMMain.cpp).</summary>
    private long Claim(Pos p, int company)
    {
        var board = Board;
        int cell = Board.CellForCompany(company);
        board[p] = cell;
        long value = SquareValue;
        foreach (var n in board.Neighbours(p))
        {
            if (board[n] == Board.Circle)
            {
                value += CircleBonus;
            }
            else if (board[n] == Board.Diamond)
            {
                value += DiamondBonus;
                board[n] = cell;
            }
        }
        return value;
    }

    private void Found(Pos p, List<GameEvent> events)
    {
        var company = Companies.First(c => !c.Exists);
        company.Exists = true;
        company.Value = Claim(p, company.Index);

        foreach (int i in Recipients) _players[i].Shares[company.Index] = 0;
        CurrentPlayer.Shares[company.Index] = FreeShares;
        CurrentPlayer.Money += company.Value;

        events.Add(new CompanyFoundedEvent(CurrentPlayerIndex, company.Index, company.Value, FreeShares, company.Value));
        CheckSplit(company.Index, events);
    }

    private void Expand(Pos p, int companyIndex, List<GameEvent> events)
    {
        var company = Companies[companyIndex];
        long added = Claim(p, companyIndex);
        company.Value += added;

        var dividends = new List<Payout>();
        foreach (int i in Recipients)
        {
            BigInteger amount = _players[i].Shares[companyIndex] * added;
            _players[i].Money += amount;
            if (amount > 0) dividends.Add(new Payout(i, amount));
        }

        events.Add(new CompanyExpandedEvent(CurrentPlayerIndex, companyIndex, added, company.Value, dividends));
        CheckSplit(companyIndex, events);
    }

    private void Merge(int survivor, int absorbed, List<GameEvent> events)
    {
        var a = Companies[survivor];
        var b = Companies[absorbed];
        long absorbedValue = b.Value;
        a.Value += b.Value;

        var rows = new List<MergerRow>();
        foreach (int i in Recipients)
        {
            var pl = _players[i];
            BigInteger profit = pl.Shares[absorbed] * absorbedValue;
            rows.Add(new MergerRow(i, pl.Shares[survivor], pl.Shares[absorbed], profit));
            pl.Money += profit;
            pl.Shares[survivor] += pl.Shares[absorbed];
            pl.Shares[absorbed] = 0;
        }

        events.Add(new MergerEvent(CurrentPlayerIndex, survivor, absorbed, absorbedValue, a.Value, rows));
    }

    private void CheckSplit(int companyIndex, List<GameEvent> events)
    {
        var company = Companies[companyIndex];
        while (company.Value >= SplitThreshold)
        {
            long old = company.Value;
            company.Value /= 2;
            var rows = new List<SplitRow>();
            foreach (int i in Recipients)
            {
                BigInteger before = _players[i].Shares[companyIndex];
                _players[i].Shares[companyIndex] = before * 2;
                rows.Add(new SplitRow(i, before, before * 2));
            }
            events.Add(new SplitEvent(companyIndex, old, company.Value, rows));
        }
    }

    // ------------------------------------------------------------------ trading

    public bool CanTrade => Phase == GamePhase.Trading;

    /// <summary>How many shares the current player can afford.</summary>
    public BigInteger MaxBuy(int company)
    {
        var c = Companies[company];
        return c.Exists && c.Value > 0 ? CurrentPlayer.Money / c.Value : 0;
    }

    /// <summary>Validates a trade for the current player. Returns an error message or null.</summary>
    public string? ValidateTrade(int company, BigInteger quantity, bool isBuy)
    {
        if (!CanTrade) return "You can only trade after choosing a square.";
        if (company < 0 || company >= Companies.Length || !Companies[company].Exists) return "That company doesn't exist.";
        if (quantity < 1) return "Enter a number of shares.";
        if (isBuy && quantity > MaxBuy(company)) return "You can't buy that many shares, you fraudster.";
        if (!isBuy && quantity > CurrentPlayer.Shares[company]) return "You can't sell that many shares, you fraudster.";
        return null;
    }

    public TradeEvent Buy(int company, BigInteger quantity)
    {
        if (ValidateTrade(company, quantity, isBuy: true) is string error) throw new InvalidOperationException(error);
        long price = Companies[company].Value;
        CurrentPlayer.Shares[company] += quantity;
        CurrentPlayer.Money -= quantity * price;
        return new TradeEvent(CurrentPlayerIndex, company, quantity, price, IsBuy: true);
    }

    public TradeEvent Sell(int company, BigInteger quantity)
    {
        if (ValidateTrade(company, quantity, isBuy: false) is string error) throw new InvalidOperationException(error);
        long price = Companies[company].Value;
        CurrentPlayer.Shares[company] -= quantity;
        CurrentPlayer.Money += quantity * price;
        return new TradeEvent(CurrentPlayerIndex, company, quantity, price, IsBuy: false);
    }

    // ------------------------------------------------------------------ scoring

    public BigInteger ShareWorth(int player)
    {
        var companies = WorldFor(player).Companies;
        BigInteger worth = 0;
        for (int c = 0; c < companies.Length; c++)
            if (companies[c].Exists)
                worth += _players[player].Shares[c] * companies[c].Value;
        return worth;
    }

    public BigInteger NetWorth(int player) => _players[player].Money + ShareWorth(player);

    /// <summary>Final standings, richest first.</summary>
    public IReadOnlyList<Standing> Standings() =>
        Enumerable.Range(0, _players.Length)
            .Select(i => new Standing(i, _players[i].Name, _players[i].Kind, _players[i].Money, ShareWorth(i)))
            .OrderByDescending(s => s.NetWorth)
            .ToList();

    /// <summary>The player holding the most shares in a company, if anyone holds any.</summary>
    public (int Player, BigInteger Shares)? MajorShareholder(int company)
    {
        int best = -1;
        BigInteger most = 0;
        for (int i = 0; i < _players.Length; i++)
        {
            if (Type == GameType.Tournament && i != CurrentPlayerIndex) continue;
            if (_players[i].Shares[company] > most)
            {
                most = _players[i].Shares[company];
                best = i;
            }
        }
        return best < 0 ? null : (best, most);
    }
}
