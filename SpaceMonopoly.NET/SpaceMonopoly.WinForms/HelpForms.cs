namespace SpaceMonopoly.WinForms;

/// <summary>How to Play (adapted from the original docs.html) and About.</summary>
internal static class HelpForms
{
    private const string Rules = """
        THE AIM
        The aim of Space Monopoly is to make money. Lots of it. You start with ₲6,000 (Galactic Bucks — you can change the currency in Options > Preferences). When companies are created you can buy shares in them, and as companies grow and merge you make more money. At the end of the game the player with the most money (including the worth of their shares) is the winner. Any number can play, but 2-4 is recommended. Computer players can make up the numbers.

        GETTING STARTED
        Before you start you'll want a decent grid — one with lots of planets (circles) close together. Use Options > Create New Grid until you like the look of one, and pick a grid size. If it's your first game, use the small grid.
        Then choose Game > New Game, add the players in the order they'll play (each can be a human, a computer at Easy, Medium or Hard, or Claude), pick a game type and click Go!
          • Modern: a quicker game (about 15-30 minutes) with new rules — see MODERN RULES below.
          • Classic: the original 2001 rules. Everyone uses the same grid and companies.
          • Tournament: the original rules, but everyone starts from the same grid and plays on their own copy of it.

        YOUR TURN
        Five squares are offered each turn (they flash on the board). Click one of them. Each offered square is marked with what it will do: a company's letter if it grows that company, M for a merger, + to found a new company, or a star if nothing happens. Point at a square to see the change in share price and in each player's net worth.
          • Next to a planet or star (but not a company): a new company is founded. You get 5 free shares and cash equal to the starting share price.
          • Next to one company: the company grows. Its share price rises by 100, plus 500 for each planet next to the square, plus 100 for each star (which it absorbs). Every shareholder gets a dividend of shares × rise.
          • Next to two or more companies: they merge (see below).
          • Anywhere else: a star is placed.
        After choosing a square you can buy and sell shares: click a company's button on the right. A white dot next to a company means you can afford at least one share. When you're done, press End Turn.

        SHARE PRICES
        Each square a company occupies adds 100 to its share price, and each square next to a planet adds 500 more.

        MERGERS
        If a square joins two companies they merge. The bigger company takes over the smaller one. If you hold shares in the smaller company, you're paid their full price in cash AND get the same number of shares in the bigger company, whose price rises by the smaller company's price. "Heck, I never said it was real life..."

        SHARE SPLITS
        When a share price reaches ₲3,200 or more it is halved and every shareholder's shares are doubled.

        END OF GAME
        Every player gets 80, 120 or 160 goes on the small, medium and large grids. If that many goes wouldn't fit on the board (for example 2 or more players on the small grid), everyone gets fewer so that about three quarters of the board gets used; the game log says how many when the game starts. The game can also end early: once there's no room left anywhere on the board to form a new company, a warning appears, the current round is finished, and then everyone has one more round. In a tournament, this happens as soon as any player's board fills up. Net wealth is your money plus the worth of your shares. A good score earns a place in the high score table.

        MODERN RULES
        Modern is a shorter game where holding the most shares matters. Everyone shares one board, which has more planets than a Classic board.
          • 25 rounds.
          • You hold a hand of 4 squares that only you can see. Play one each turn, then draw a new one. Squares that can no longer be played are replaced automatically. Each turn, before you play, you can swap one square: right-click it to throw it away and draw another. Once per game you can Prospect: throw away your whole hand and draw a new one.
          • When a company grows, its share price rises, but there is no cash dividend.
          • Founding a company gives you 3 free shares and no cash.
          • You can buy up to 100 shares a turn, across all companies. Selling is unlimited.
          • Cash earns 3% interest at the start of each of your turns, so it can be worth holding some back.
          • Events: at the start of each round an event card is flipped, and the next round's card is shown in advance in the side panel, so you can prepare. Pirate Raid (the most expensive company loses 25%), Asteroid Strike (a company loses its newest square and 600), Market Boom (every price +10%), Solar Flare (no company can grow this round), Planet Discovered, Merger Mania (merger bonuses doubled), Insider Scandal (the leader's biggest holding loses 20%), Galactic Grant (5,000 for the poorest player) and Tax Audit (players above the average pay 10% of their cash). The rest are Quiet Quarters. Choose Calm, Standard or Wild in New Game to set how many.
          • Each company has 1,000 shares, shared by all players. The company buttons show how many are left. Shares never split in a Modern game.
          • In a merger, shares in the company taken over are bought back at its share price; you don't get shares in the bigger company. Its largest shareholder gets a bonus of 10 times its share price, and the second-largest 5 times. You need at least 10 shares to qualify for a bonus. Players tied for largest share both bonuses; players tied for second share the second bonus.
          • Catching up: while you're alone in last place you hold 5 squares instead of 4.
          • At the end of the game, every company still in play pays its two largest shareholders (with at least 10 shares) 10 and 5 times its share price, just like a merger.
          • Each player is dealt a secret goal at the start (yours is shown in the side panel), worth 4,000-20,000 if you meet it. All goals are revealed at the end.
          • Modern scores have their own high score tables.

        MULTIPLAYER OPTIONS (Options > Preferences)
          • Force mergers / Force expansion make players take mergers or expansions when offered.
          • Allow company creation lets founding a company count as expanding.
          • Hide player details stops players looking at each other's holdings.

        HINTS AND TIPS (from the original author)
          • Buy lots of shares in small (~₲200) companies. They usually grow quickly, especially next to planets.
          • Create companies close together. There's no point creating them on opposite sides of the board — they'll never merge. Remember there can only be 5 companies at once on the small grid, 8 on the medium grid and 12 on the large grid.
          • In multiplayer games make sure it's you who creates the expensive (₲1,100+) companies.
          • Think before merging: if your opponent holds more of the smaller company, they'll make more out of it.
          • Late on, sell shares in companies that can't grow and buy into the big companies, which are more likely to grow.

        COMPUTER PLAYERS
          • Easy plays mostly at random. Medium picks the square that looks best right now and buys the best-value shares. Hard thinks through each square and its trades, sells shares that have stopped growing and watches the leading opponent.
          • Claude players ask Claude, through your Claude Code login, for each move. A short comment explaining the move appears in the game log. Each move takes 10-30 seconds; settings are under Options > Preferences > Claude.
        """;

    public static void ShowHowToPlay(IWin32Window owner)
    {
        using var form = Ui.Dialog("How to Play Space Monopoly", 640, 560);
        form.FormBorderStyle = FormBorderStyle.Sizable;
        form.MinimizeBox = false;
        var text = new RichTextBox
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            BorderStyle = BorderStyle.None,
            BackColor = SystemColors.Window,
            Font = new Font("Segoe UI", 10f),
            Text = Rules,
        };
        var ok = Ui.Button("Close");
        ok.DialogResult = DialogResult.OK;
        form.AcceptButton = form.CancelButton = ok;
        var holder = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = SystemColors.Window };
        holder.Controls.Add(text);
        form.Controls.Add(holder);
        form.Controls.Add(Ui.ButtonBar(ok));
        form.ResumeLayout(false);
        form.ShowDialog(owner);
    }

    public static void ShowAbout(IWin32Window owner)
    {
        string version = typeof(HelpForms).Assembly.GetName().Version?.ToString(2) ?? "2.0";
        Dialogs.Show(owner, "About Space Monopoly", Color.MidnightBlue,
            [
                $"Space Monopoly {version} — the freeware game of buying and selling shares.",
                "Original game © 1998-2001 David Vivash (Borland C++ Builder), released under the GNU GPL v2.",
                "Windows 11 edition by Jerame Chamberlain: rebuilt in C# / .NET with computer and Claude opponents.",
            ]);
    }
}
