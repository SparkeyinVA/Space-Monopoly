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
public sealed partial class Game
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
    private readonly GameRandom _rng;
    private List<Pos> _choices = [];

    // Modern only: each player's hand of squares, whether they've used their one prospect, and shares bought this turn.
    private readonly List<Pos>[] _hands;
    private readonly bool[] _prospected;
    private BigInteger _boughtThisTurn;

    public Game(GridSpec spec, Board startingBoard, IEnumerable<(string Name, PlayerKind Kind)> players,
        GameType type, GameRules rules, int? seed = null)
    {
        _players = players.Select(p => new Player(p.Name, p.Kind, StartingMoney, spec.Companies)).ToArray();
        if (_players.Length == 0) throw new ArgumentException("A game needs at least one player.", nameof(players));
        if (startingBoard.Width != spec.Width || startingBoard.Height != spec.Height)
            throw new ArgumentException("Board size doesn't match the grid size.", nameof(startingBoard));

        Spec = spec;
        // Tournament only makes sense with two or more players (Players.cpp).
        Type = _players.Length == 1 && type == GameType.Tournament ? GameType.Classic : type;
        Rules = rules;
        _rng = GameRandom.FromSeed(seed);

        var board = startingBoard.Clone();
        board.ClearPieces();
        _worlds = SharedBoard
            ? [new World(board, Company.CreateSet(spec.Companies))]
            : _players.Select(_ => new World(board.Clone(), Company.CreateSet(spec.Companies))).ToArray();

        GoesPerPlayer = GoesThatFit(MaxGoes, board.CountOf(Board.Empty), SharedBoard ? _players.Length : 1);

        _hands = _players.Select(_ => new List<Pos>()).ToArray();
        _prospected = new bool[_players.Length];
        if (IsModern)
        {
            for (int i = 0; i < _players.Length; i++) FillHand(i);
            InitEvents();
        }

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
        _hands = other._hands.Select(h => new List<Pos>(h)).ToArray();
        _prospected = (bool[])other._prospected.Clone();
        _boughtThisTurn = other._boughtThisTurn;
        InterestThisTurn = other.InterestThisTurn;
        CopyEvents(other);
        _rng = other._rng.Fork();
        CurrentPlayerIndex = other.CurrentPlayerIndex;
        Turn = other.Turn;
        GoesPerPlayer = other.GoesPerPlayer;
        _finalRound = other._finalRound;
        Phase = other.Phase;
    }

    public GridSpec Spec { get; }
    public GameType Type { get; }
    public GameRules Rules { get; }

    /// <summary>True for the Modern rules: a hand of squares, limited buying and majority bonuses.</summary>
    public bool IsModern => Type == GameType.Modern;

    /// <summary>Classic and Modern players share one board; in a tournament everyone has their own.</summary>
    public bool SharedBoard => Type != GameType.Tournament;

    /// <summary>Goes per player before any reduction to fit the board.</summary>
    public int MaxGoes => IsModern ? ModernRules.Rounds : Spec.GoesPerPlayer;
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
    public bool GoesReduced => GoesPerPlayer < MaxGoes;

    /// <summary>Goes per player that fit on a board with this many empty squares shared by this many players.</summary>
    public static int GoesThatFit(GridSpec spec, int emptySquares, int playersSharingBoard) =>
        GoesThatFit(spec.GoesPerPlayer, emptySquares, playersSharingBoard);

    private static int GoesThatFit(int maxGoes, int emptySquares, int playersSharingBoard)
    {
        int fit = (int)(emptySquares * BoardFillLimit) / Math.Max(1, playersSharingBoard);
        return Math.Clamp(fit, 1, maxGoes);
    }

    public GamePhase Phase { get; private set; }

    /// <summary>The squares on offer this turn (the flashing blue squares).</summary>
    public IReadOnlyList<Pos> Choices => _choices;

    public World CurrentWorld => WorldFor(CurrentPlayerIndex);
    public Board Board => CurrentWorld.Board;
    public Company[] Companies => CurrentWorld.Companies;

    public World WorldFor(int player) => SharedBoard ? _worlds[0] : _worlds[player];

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

    /// <summary>Modern: the interest the current player was paid at the start of this turn.</summary>
    public BigInteger InterestThisTurn { get; private set; }

    private void BeginTurn()
    {
        _boughtThisTurn = 0;
        InterestThisTurn = IsModern ? CurrentPlayer.Money * ModernRules.InterestPercent / 100 : 0;
        CurrentPlayer.Money += InterestThisTurn;
        _choices = IsModern ? RefreshHand(CurrentPlayerIndex) : GenerateChoices();
        Phase = _choices.Count == 0 ? GamePhase.Over : GamePhase.ChoosingSquare;
    }

    /// <summary>
    /// True if the square can be offered: it's empty, and it wouldn't found a company when every company
    /// is already in play.
    /// </summary>
    private bool CanOffer(Pos p, bool canFound)
    {
        if (Board[p] != Board.Empty) return false;
        if (canFound) return true;
        var s = Board.Survey(p);
        return !(s.NextToCircleOrDiamond && s.Companies.Count == 0);
    }

    /// <summary>Picks up to five empty squares to offer (go() in SMMain.cpp).</summary>
    private List<Pos> GenerateChoices()
    {
        bool canFound = Companies.Any(c => !c.Exists);
        var candidates = Board.AllPositions().Where(p => CanOffer(p, canFound)).ToList();

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
        RoundStartEvents = [];

        if (Turn >= Rounds)
        {
            Phase = GamePhase.Over;
            _choices = [];
            return true;
        }

        if (IsModern && CurrentPlayerIndex == 0) StartRound();

        BeginTurn();
        return Phase == GamePhase.Over;
    }

    // ------------------------------------------------------------------ Modern: hands of squares

    /// <summary>The squares a player holds (Modern). Only show a player their own hand.</summary>
    public IReadOnlyList<Pos> HandOf(int player) => _hands[player];

    /// <summary>True once a player has used their one prospect.</summary>
    public bool HasProspected(int player) => _prospected[player];

    /// <summary>True if the current player may still prospect: throw away their hand and draw a new one (once per game).</summary>
    public bool CanProspect => IsModern && Phase == GamePhase.ChoosingSquare && !_prospected[CurrentPlayerIndex];

    /// <summary>Throws away the current player's hand and draws a new one before they play (once per game).</summary>
    public void Prospect()
    {
        if (!CanProspect) throw new InvalidOperationException("You can prospect only once per game, before you play a square.");
        _prospected[CurrentPlayerIndex] = true;
        var old = _hands[CurrentPlayerIndex].ToList();
        _hands[CurrentPlayerIndex].Clear();
        FillHand(CurrentPlayerIndex, exclude: old);
        if (_hands[CurrentPlayerIndex].Count == 0) _hands[CurrentPlayerIndex].AddRange(old); // nothing new to draw
        _choices = [.. _hands[CurrentPlayerIndex]];
    }

    /// <summary>True if playing the square would found, grow or merge a company (rather than only place a star).</summary>
    private bool IsLive(Pos p)
    {
        var s = Board.Survey(p);
        return s.Companies.Count > 0 || s.NextToCircleOrDiamond;
    }

    /// <summary>Throws away squares that can no longer be played, tops the hand up and returns it.</summary>
    private List<Pos> RefreshHand(int player)
    {
        bool canFound = Companies.Any(c => !c.Exists);
        _hands[player].RemoveAll(p => !CanOffer(p, canFound));
        FillHand(player);
        return [.. _hands[player]];
    }

    /// <summary>Draws random squares no one holds until the player has a full hand (or none are left).</summary>
    private void FillHand(int player, IReadOnlyCollection<Pos>? exclude = null)
    {
        var hand = _hands[player];
        int missing = ModernRules.HandSize - hand.Count;
        if (missing <= 0) return;

        bool canFound = Companies.Any(c => !c.Exists);
        var held = _hands.SelectMany(h => h).ToHashSet();
        var open = Board.AllPositions()
            .Where(p => !held.Contains(p) && (exclude is null || !exclude.Contains(p)) && CanOffer(p, canFound))
            .ToList();
        // Draw squares that do something (touch a planet, star or company). Otherwise hands fill up with
        // squares that only place a star in empty space. Plain squares are drawn only when nothing else is left.
        var pool = open.Where(IsLive).ToList();
        if (pool.Count == 0) pool = open;
        for (int i = 0; i < missing && pool.Count > 0; i++)
        {
            int j = _rng.Next(pool.Count);
            hand.Add(pool[j]);
            pool[j] = pool[^1];
            pool.RemoveAt(pool.Count - 1);
        }
    }

    /// <summary>Offers just this square, so a copy of the game can show what it would do (see <see cref="MovePreview"/>).</summary>
    internal void OfferOnlyForPreview(Pos p)
    {
        _choices = [p];
        Phase = GamePhase.ChoosingSquare;
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

        // Modern: play one square, then draw one.
        if (IsModern)
        {
            _hands[CurrentPlayerIndex].Remove(p);
            FillHand(CurrentPlayerIndex);
        }

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
        SharedBoard ? Enumerable.Range(0, _players.Length) : [CurrentPlayerIndex];

    private void Act(Pos p, List<GameEvent> events)
    {
        var board = Board;
        var around = board.Survey(p);

        if (SolarFlare && around.Companies.Count > 0)
        {
            board[p] = Board.Diamond;
            events.Add(new DiamondPlacedEvent(CurrentPlayerIndex, p));
            return;
        }

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
                Companies[absorbed].Supply = 0;

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
        RecordClaim(p);
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
                RecordClaim(n);
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

        // Modern: fewer free shares, no cash, and a fixed number of shares to go round.
        long freeShares = IsModern ? ModernRules.FreeShares : FreeShares;
        long bonus = IsModern ? 0 : company.Value;
        company.Supply = IsModern ? ModernRules.SharesPerCompany : 0;
        CurrentPlayer.Shares[company.Index] = freeShares;
        CurrentPlayer.Money += bonus;

        events.Add(new CompanyFoundedEvent(CurrentPlayerIndex, company.Index, company.Value, freeShares, bonus));
        CheckSplit(company.Index, events);
    }

    private void Expand(Pos p, int companyIndex, List<GameEvent> events)
    {
        var company = Companies[companyIndex];
        long added = Claim(p, companyIndex);
        company.Value += added;

        // Modern: growth raises the share price only. There is no cash dividend.
        var dividends = new List<Payout>();
        foreach (int i in IsModern ? [] : Recipients)
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

        // Modern: shares in the company taken over are bought out for cash (no free shares in the survivor),
        // and its two largest holders get a bonus.
        var bonuses = IsModern
            ? MajorityBonuses(Recipients.Select(i => (i, _players[i].Shares[absorbed])), absorbedValue * (MergerMania ? 2 : 1))
            : [];

        var rows = new List<MergerRow>();
        foreach (int i in Recipients)
        {
            var pl = _players[i];
            BigInteger profit = pl.Shares[absorbed] * absorbedValue;
            BigInteger bonus = bonuses.GetValueOrDefault(i);
            rows.Add(new MergerRow(i, pl.Shares[survivor], pl.Shares[absorbed], profit, bonus));
            pl.Money += profit + bonus;
            if (!IsModern) pl.Shares[survivor] += pl.Shares[absorbed];
            pl.Shares[absorbed] = 0;
        }

        events.Add(new MergerEvent(CurrentPlayerIndex, survivor, absorbed, absorbedValue, a.Value, rows));
    }

    /// <summary>
    /// Modern merger bonuses: the largest holder gets <see cref="ModernRules.LargestHolderBonus"/> times the share price
    /// and the second-largest <see cref="ModernRules.SecondHolderBonus"/> times. Players tied for largest share both
    /// bonuses evenly; players tied for second share the second bonus. A sole holder gets both.
    /// </summary>
    internal static Dictionary<int, BigInteger> MajorityBonuses(IEnumerable<(int Player, BigInteger Shares)> holdings, long price)
    {
        var result = new Dictionary<int, BigInteger>();
        var holders = holdings.Where(h => h.Shares > 0).OrderByDescending(h => h.Shares).ToList();
        if (holders.Count == 0) return result;

        BigInteger first = ModernRules.LargestHolderBonus * price, second = ModernRules.SecondHolderBonus * price;
        var top = holders.Where(h => h.Shares == holders[0].Shares).ToList();
        var rest = holders.Skip(top.Count).ToList();

        if (top.Count > 1 || rest.Count == 0)
        {
            foreach (var h in top) result[h.Player] = (first + second) / top.Count;
            return result;
        }

        result[top[0].Player] = first;
        var runnersUp = rest.Where(h => h.Shares == rest[0].Shares).ToList();
        foreach (var h in runnersUp) result[h.Player] = second / runnersUp.Count;
        return result;
    }

    private void CheckSplit(int companyIndex, List<GameEvent> events)
    {
        var company = Companies[companyIndex];
        while (company.Value >= SplitThreshold)
        {
            long old = company.Value;
            company.Value /= 2;
            company.Supply *= 2;   // Modern: every holder's shares double, and so does the supply
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

    /// <summary>
    /// How many shares the current player can buy: what they can afford, and in Modern no more than
    /// the shares left in the company and their buying allowance for this turn.
    /// </summary>
    public BigInteger MaxBuy(int company)
    {
        var c = Companies[company];
        if (!c.Exists || c.Value <= 0) return 0;
        var afford = CurrentPlayer.Money / c.Value;
        if (!IsModern) return afford;
        return BigInteger.Max(0, BigInteger.Min(afford, BigInteger.Min(BuyAllowanceLeft, SharesLeft(company))));
    }

    /// <summary>Modern: shares the current player may still buy this turn, across all companies.</summary>
    public BigInteger BuyAllowanceLeft => IsModern ? BigInteger.Max(0, ModernRules.BuyLimitPerTurn - _boughtThisTurn) : BigInteger.MinusOne;

    /// <summary>Modern: shares in a company that no player holds yet. -1 when there's no limit.</summary>
    public BigInteger SharesLeft(int company)
    {
        var c = Companies[company];
        if (!IsModern || c.Supply == 0) return BigInteger.MinusOne;
        BigInteger held = 0;
        for (int i = 0; i < _players.Length; i++)
            if (WorldFor(i) == CurrentWorld) held += _players[i].Shares[company];
        return BigInteger.Max(0, c.Supply - held);
    }

    /// <summary>Validates a trade for the current player. Returns an error message or null.</summary>
    public string? ValidateTrade(int company, BigInteger quantity, bool isBuy)
    {
        if (!CanTrade) return "You can only trade after choosing a square.";
        if (company < 0 || company >= Companies.Length || !Companies[company].Exists) return "That company doesn't exist.";
        if (quantity < 1) return "Enter a number of shares.";
        if (isBuy && IsModern && quantity > MaxBuy(company) && CurrentPlayer.Money / Companies[company].Value >= quantity)
            return BuyAllowanceLeft < quantity
                ? $"You can buy only {ModernRules.BuyLimitPerTurn} shares a turn ({BuyAllowanceLeft} left this turn)."
                : $"There are only {SharesLeft(company)} {Companies[company].Name} shares left.";
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
        _boughtThisTurn += quantity;
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
