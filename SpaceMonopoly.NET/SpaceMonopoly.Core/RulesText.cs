namespace SpaceMonopoly.Core;

/// <summary>
/// How to Play for one game type (F10), built from the rule constants so the text can't drift from the code
/// and Modern's page never shows Classic rules.
/// </summary>
public static class RulesText
{
    public static string For(GameType type, Currency? currency = null)
    {
        var c = currency ?? Currency.Default;
        bool modern = type == GameType.Modern;
        var t = new System.Text.StringBuilder();
        void Line(string s = "") => t.AppendLine(s);

        Line("THE AIM");
        Line($"Make money. Lots of it. You start with {c.Format(Game.StartingMoney)} (Galactic Bucks; you can change the currency in Options > Preferences). " +
             "Found companies, buy their shares, and profit as they grow and merge. The player with the highest net worth at the end wins: cash plus " +
             "shares at their final price" + (modern ? ", after the end-of-game payouts and secret goals." : ".") +
             " Computer and Claude players can make up the numbers.");
        Line();

        Line("THIS GAME TYPE");
        Line(type switch
        {
            GameType.Modern => $"Modern: a quicker game (about 15-30 minutes) where holding the most shares matters. Everyone shares one board, " +
                               $"which has more planets than a Classic board. {ModernRules.Rounds} rounds.",
            GameType.Tournament => "Tournament: the original 2001 rules, but everyone starts from the same grid and plays on their own copy of it.",
            _ => "Classic: the original 2001 rules. Everyone uses the same grid and companies.",
        });
        Line("Start a game with Game > New Game: add the players in the order they'll play, pick a game type and click Go! " +
             "Use Options > Create New Grid until you like the look of the board; lots of planets close together is best.");
        Line();

        Line("YOUR TURN");
        Line(modern
            ? $"You hold a hand of {ModernRules.HandSize} squares that only you can see (they flash on the board). Play one, then you draw a new one. " +
              "Squares that can no longer be played are replaced automatically. Your hand is listed in the side panel: each square shows what it would do, with Play and Swap buttons. Before you play " +
              "you may swap one square a turn (Swap, or right-click it on the board), and once per game you may take a New hand: " +
              "throw away all your squares and draw new ones."
            : $"{Game.ChoiceCount} squares are offered each turn (they flash on the board). Click one of them.");
        Line("Each square is marked with what it will do: a company's letter if it grows that company, M for a merger, + to found a new company, " +
             "or a star if nothing happens. Point at a square to see the change in share price and in each player's net worth.");
        Line(modern
            ? $"  • Next to a planet or star (but not a company): you found a new company and get {ModernRules.FreeShares} free shares (no cash)."
            : $"  • Next to a planet or star (but not a company): you found a new company and get {Game.FreeShares} free shares plus cash equal to the starting share price.");
        Line($"  • Next to one company: it grows. Its share price rises by {Game.SquareValue}, plus {Game.CircleBonus} for each planet next to the square, " +
             $"plus {Game.DiamondBonus} for each star (which it absorbs)." +
             (modern ? " There is no dividend: you gain through the higher share price." : " Every shareholder also gets a cash dividend of shares × rise."));
        Line("  • Next to two or more companies: they merge (see MERGERS).");
        Line("  • Anywhere else: a star is placed.");
        Line(modern
            ? $"Then buy and sell shares: up to {ModernRules.BuyLimitPerTurn} shares a turn across all companies, and any number sold. " +
              $"A new company has {ModernRules.SharesPerCompany:N0} shares shared by all players; the company buttons show how many are left (or \"sold out\"). Press End Turn when you're done."
            : "Then buy and sell any number of shares: click a company's button on the right. Press End Turn when you're done.");
        Line();

        Line("MERGERS");
        if (modern)
        {
            Line("  • When: you play an M square, one that touches two or more companies.");
            Line("  • Which survives: the company with more squares. On a tie, the earlier letter. The other is taken over and disappears.");
            Line("  • If you hold shares in the company taken over: you're paid its share price in cash for each share, and those shares are gone. " +
                 $"Its largest holder also gets a bonus of {ModernRules.LargestHolderBonus}× its share price and the second-largest {ModernRules.SecondHolderBonus}×. " +
                 $"You need at least {ModernRules.MinSharesForBonus} shares to qualify. Players tied for largest share both bonuses; players tied for second share the second bonus. " +
                 "The multiples double each time the company has split (20× and 10× after one split, 40× and 20× after two), so splits don't shrink the prizes.");
            Line("  • Then you may turn any part of that money into shares of the surviving company, at its new price. This doesn't use your buying allowance; " +
                 "whatever you don't convert stays as cash.");
            Line($"  • If you hold shares in the surviving company: you get no cash, but its share price rises by half the absorbed company's price, " +
                 "plus the merger square's own growth.");
            long a = 2000, b = 1200;
            long newA = a + b / ModernRules.MergerPriceDivisor + Game.SquareValue;
            long paid12 = 12 * b + ModernRules.LargestHolderBonus * b, paid10 = 10 * b + ModernRules.SecondHolderBonus * b;
            Line($"  • Example: Atlantis has 6 squares at {c.Format(a)} a share, Cannon 3 squares at {c.Format(b)}. You play an M square between them (no planets nearby). " +
                 $"Atlantis takes over Cannon. The holder of 12 Cannon shares (largest) is paid {c.Format(12 * b)} plus a {c.Format(ModernRules.LargestHolderBonus * b)} bonus = {c.Format(paid12)}; " +
                 $"the holder of 10 (second) gets {c.Format(10 * b)} plus {c.Format(ModernRules.SecondHolderBonus * b)} = {c.Format(paid10)}. " +
                 $"Atlantis shares rise from {c.Format(a)} to {c.Format(newA)} (half of {c.Format(b)}, plus {Game.SquareValue} for the square), so 8 Atlantis shares gain {c.Format(8 * (newA - a))}. " +
                 $"The largest Cannon holder could turn their {c.Format(paid12)} into up to {paid12 / newA} Atlantis shares.");
            Line("  • Merger Mania (an event) doubles the bonuses for a round.");
        }
        else
        {
            Line("If a square joins two companies they merge. The bigger company takes over the smaller one. If you hold shares in the smaller company, " +
                 "you're paid their full price in cash AND get the same number of shares in the bigger company, whose price rises by the smaller company's price. " +
                 "\"Heck, I never said it was real life...\"");
            Line();
            Line("SHARE SPLITS");
            Line($"When a share price reaches {c.Format(Game.SplitThreshold)} or more it is halved and every shareholder's shares are doubled.");
        }
        Line();

        if (modern)
        {
            Line("SHARE SPLITS");
            Line($"A company splits when it is established: the first time it has {ModernRules.FirstSplitSquares} or more squares AND a share price of " +
                 $"{c.Format(ModernRules.FirstSplitPrice)} or more, the second time at {ModernRules.SecondSplitSquares}+ squares AND {c.Format(ModernRules.SecondSplitPrice)}+. " +
                 $"A split halves the price, doubles every holder's shares and doubles the number of shares the company has, so it can never sell out for good. " +
                 "After two splits it's a blue chip and never splits again. The company buttons and the Market show how close each company is.");
            Line();
            Line("SPECIAL SQUARES (medium and large grids)");
            Line($"  • Nebula (purple cloud): a company square next to it gets double the planet bonus ({2 * Game.CircleBonus} a planet instead of {Game.CircleBonus}).");
            Line("  • Black hole: squares next to it can't be played.");
            Line("  • Wormhole (two cyan swirls): the two ends count as next to each other, so companies can grow and merge across the board through them.");
            Line();
            Line("CASH, EVENTS AND CATCHING UP");
            Line($"  • Cash earns {ModernRules.InterestPercent}% interest at the start of each of your turns, so it can be worth holding some back.");
            Line("  • Events: from round 3, an event card is flipped at the start of each round, and the next round's card is always shown in advance " +
                 "in the side panel. Pirate Raid, Asteroid Strike, Market Boom, Solar Flare (no company can grow this round), Planet Discovered, " +
                 "Merger Mania, Insider Scandal, Galactic Grant, Tax Audit and Dividend Day; the rest are Quiet Quarters. Choose Calm, Standard or Wild in New Game.");
            Line($"  • Dividend Day pays every player {EventCards.DividendPerCompany:N0} for each company they hold at least one share in (not per share), " +
                 "so holding a share in several companies before it pays off, and helps most when you're behind.");
            Line($"  • While you're alone in last place you hold {ModernRules.HandSize + 1} squares, and the extra one helps a company you hold or founds one.");
            Line($"  • A square you hold for {ModernRules.SquareExpiryTurns} of your turns without playing it expires at the end of the last one and is replaced. " +
                 "The hand list shows the turns left on each square.");
            Line("  • Each player is dealt a secret goal (yours is shown in the side panel), revealed at the end with its reward.");
            Line();
        }

        Line("END OF GAME");
        if (modern)
        {
            Line($"After {ModernRules.Rounds} rounds (or one round after there's no room left for a new company). Then every company of " +
                 $"{ModernRules.FinalPayoutMinSquares} or more squares pays its two largest holders (at least {ModernRules.MinSharesForBonus} shares) " +
                 $"{(double)ModernRules.LargestHolderBonus / ModernRules.FinalPayoutDivisor}× and {(double)ModernRules.SecondHolderBonus / ModernRules.FinalPayoutDivisor}× " +
                 $"its share price, half a merger bonus, doubled for each split the company has had. A sole qualifying holder gets only the first. Shares bought in the last {ModernRules.LateBuyRounds} rounds " +
                 "don't count for this ranking. Secret goals are paid, and the highest net worth wins. Modern scores have their own high score tables.");
        }
        else
        {
            Line($"Every player gets {GridSpec.For(GridSize.Small).GoesPerPlayer}, {GridSpec.For(GridSize.Medium).GoesPerPlayer} or {GridSpec.For(GridSize.Large).GoesPerPlayer} " +
                 "goes on the small, medium and large grids, or fewer if that many wouldn't fit on the board (the game log says how many). " +
                 "The game also ends one round after there's no room left anywhere to form a new company" +
                 (type == GameType.Tournament ? " (on any player's board)" : "") + ". The highest net worth wins.");
        }
        Line();

        Line("MULTIPLAYER OPTIONS (Options > Preferences)");
        Line("  • Force mergers / Force expansion make players take mergers or expansions when offered.");
        Line("  • Allow company creation lets founding a company count as expanding.");
        Line("  • Hide player details stops players looking at each other's holdings.");
        Line();

        Line("HINTS AND TIPS");
        if (modern)
        {
            Line("  • Judge a move by your gain compared with the leader's: helping a rival a little is fine if it helps you more.");
            Line($"  • Race for the top two places (at least {ModernRules.MinSharesForBonus} shares) in companies likely to be taken over: their bonuses are the big prizes.");
            Line("  • Holding the company that survives a merger pays too, through its price jump.");
            Line("  • Use your hand to plan: keep a merger square until you lead the company it would take over.");
            Line("  • Watch next round's event and the buy limit when deciding how much cash to keep.");
        }
        else
        {
            Line("  • Buy lots of shares in small (~₲200) companies. They usually grow quickly, especially next to planets.");
            Line("  • Create companies close together, or they'll never merge. There can be 5 companies at once on the small grid, 8 on the medium grid and 12 on the large grid.");
            Line("  • Think before merging: if your opponent holds more of the smaller company, they'll make more out of it.");
            Line("  • Late on, sell shares in companies that can't grow and buy into those that can.");
        }
        Line();

        Line("COMPUTER PLAYERS");
        Line("  • Easy plays mostly at random. Medium picks the square that looks best right now and buys the best-value shares. Normal looks ahead like Hard " +
             "but sometimes settles for its second-best square, and trades like Medium. Hard thinks through each square and its trades and watches the leading opponent.");
        Line("  • Each computer or Claude player can have a personality (chosen in New Game, or Random): Raider goes after mergers and majority bonuses, " +
             "Tycoon founds companies and holds them, Hoarder keeps cash and avoids risk, Gambler bets big on one company. It changes how they play, not how well.");
        Line("  • Claude players ask Claude, through your Claude Code login, for each move, and explain it in the game log. Each move takes 10-30 seconds; " +
             "settings are under Options > Preferences > Claude. A stronger model or higher effort plays better but takes longer.");
        return t.ToString();
    }
}
