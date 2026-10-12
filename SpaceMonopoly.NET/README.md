# Space Monopoly for Windows 11

A rebuild of David Vivash's 2001 freeware share-trading game in C# / .NET 9 WinForms, with computer opponents.
Windows 11 edition by Jerame Chamberlain.
The rules are ported from the original Borland C++ Builder source in `../sourceCode/spacemonopoly`.

## Running it

**Visual Studio 2022:** open `SpaceMonopoly.sln` and press F5. `SpaceMonopoly.WinForms` is the startup project.

**Command line:**

```powershell
dotnet run --project SpaceMonopoly.WinForms
dotnet test                      # rules, AI and storage tests
dotnet publish SpaceMonopoly.WinForms -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=none -o publish
```

The published `SpaceMonopoly.exe` is portable: it includes the .NET runtime, so it runs without anything else installed.

## Players

Any mix of up to 8 players, chosen in **Game > New Game**:

| Type | How it plays |
|---|---|
| Human | Clicks a flashing square, then trades through the company buttons and presses **End Turn**. |
| Computer (Easy) | Mostly random squares and random purchases. |
| Computer (Medium) | Picks the square that looks best right away and invests in the best-value shares. It never sells. |
| Computer (Hard) | Simulates every offered square together with its follow-up trades, sells shares that have stopped growing and keeps an eye on the leading opponent. |
| Claude | Asks Claude for each move through **Claude Code** and explains its move in the game log. |

In the automated tests (30 games per pairing), Hard and Medium each beat Easy in all 30 games, and Hard beat Medium in 27 of 30.

### Claude players

Claude players run Claude Code headless (`claude -p`) with your existing Claude login, so no API key is needed.
Moves count toward your Claude plan's usage limits. Each turn makes one request covering the square and the trades,
and usually takes about 10–30 seconds.

- Requires Claude Code to be installed and signed in (`claude` on PATH or `%USERPROFILE%\.local\bin\claude.exe`).
- **Options > Preferences > Claude** sets the model (`sonnet` by default), effort, timeout and the path to `claude.exe`.
  **Test Claude** checks the connection.
- Claude Code runs from a temporary folder with every tool disabled, so it can only read the game description and answer.
  `ANTHROPIC_API_KEY` is removed from its environment so your subscription login is used.
- If Claude can't be reached or gives an invalid move, that turn is played by the Hard strategy and the reason appears in the log.

## Project layout

| Project | Contents |
|---|---|
| `SpaceMonopoly.Core` | Rules engine with no UI (`Game`, `Board`), computer players (`Ai/`), and settings and high-score storage. |
| `SpaceMonopoly.WinForms` | The game window and dialogs, built in code and scaled for high-DPI screens. |
| `SpaceMonopoly.Tests` | xUnit tests: rules, whole simulated games on every grid size, AI strength, and parsing of Claude's answers. |

Settings and high scores are saved as JSON in `%LOCALAPPDATA%\SpaceMonopoly`.

## Differences from the 2001 version

- Bug fixes in the rules:
  - Shares in an absorbed company now go to zero after a merger. Before, they kept adding to your net worth.
  - Mergers in tournament games now convert your shares.
  - A price of 6,400 or more now splits repeatedly instead of only once.
  - The game no longer hangs when fewer than five squares are left to offer.
- Every player gets 80 / 120 / 160 goes on the small / medium / large grid. In the original, those goes were shared
  by everyone. If that many goes wouldn't fit on the board (2 or more players on the small grid, for example),
  everyone gets fewer so that about 75% of the empty squares are used. The game log shows the number when the game starts.
- The game can also end early. Once no empty square could ever form a new company, a warning appears. The current round
  is finished, then one more round is played. In a tournament, this starts when it happens on any player's board.
- Up to 5 companies on the small grid, 8 on the medium grid and 12 on the large grid. The original allowed 5 on every grid.
- Money is in Galactic Bucks (₲) by default. Any currency name up to 16 characters can be set in Options > Preferences.
- Money and share counts have no upper limit, because fortunes grow very large in long games.
  Amounts of a quadrillion or more are shown in words, for example ₲4.25 quintillion.
- Starting a new game keeps the planets and clears old companies. In the original, the code meant to clear the grid didn't run.
- The high-score file is no longer "encrypted", and it now records the player type and game type.
- New: a **Modern** game type (25 rounds, a private hand of 4 squares, no dividends, a 50-share buying limit per turn,
  100 shares per company that double at each of up to two splits, majority bonuses, interest, events and secret goals).
  See Help > How to Play and the main [README](../README.md). `SpaceMonopoly.Balance` plays
  computer-vs-computer games and reports balance measures.
- New: each offered square is marked with what it will do (company letter, M for merger, + to found, star), and
  pointing at it shows the price change and each player's gain.
- New: computer and Claude players, a game log, and numbered squares on offer. The board scales to the window, and company letters are drawn on the board for colour-blind players.
