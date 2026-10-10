namespace SpaceMonopoly.Core;

/// <summary>Something that happened as a result of a move or trade. The UI turns these into dialogs or log lines.</summary>
public abstract record GameEvent;

public sealed record DiamondPlacedEvent(int Player, Pos At) : GameEvent;

/// <summary>A new company was founded. The founder gets five free shares plus cash equal to the share price.</summary>
public sealed record CompanyFoundedEvent(int Player, int Company, long SharePrice, long FreeShares, long Bonus) : GameEvent;

public sealed record Payout(int Player, BigInteger Amount);

/// <summary>A company grew by one square. Every shareholder receives a dividend of shares x value added.</summary>
public sealed record CompanyExpandedEvent(int Player, int Company, long ValueAdded, long NewValue, IReadOnlyList<Payout> Dividends) : GameEvent;

public sealed record MergerRow(int Player, BigInteger SurvivorShares, BigInteger AbsorbedShares, BigInteger Profit);

/// <summary>Two companies merged; holders of the absorbed company are paid out and get matching shares in the survivor.</summary>
public sealed record MergerEvent(int Player, int Survivor, int Absorbed, long AbsorbedValue, long NewValue, IReadOnlyList<MergerRow> Rows) : GameEvent;

public sealed record SplitRow(int Player, BigInteger Before, BigInteger After);

/// <summary>Shares worth 3,200 or more split two-for-one.</summary>
public sealed record SplitEvent(int Company, long OldValue, long NewValue, IReadOnlyList<SplitRow> Rows) : GameEvent;

public sealed record TradeEvent(int Player, int Company, BigInteger Quantity, long Price, bool IsBuy) : GameEvent
{
    public BigInteger Total => Quantity * Price;
}
