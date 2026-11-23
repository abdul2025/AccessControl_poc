namespace AccessControl.Domain.Entities;

/// <summary>
/// Controller memory allocation sent with commands 1107 / 1105 (docs/PROJECT_SPEC.md §6.3). Fixed when the controller
/// is created: changing it on a configured controller requires command 301, which erases the controller database
/// and unreported transactions (spec §3.7 rule 4a).
/// </summary>
public sealed record ScpAllocation(int Transactions, int Cards, short AccessLevelsPerCard, int GmtOffsetSeconds)
{
    /// <summary>GMT offset bounds in the SDK sign convention, seconds = -(UTC offset): UTC+14 = -50400, UTC-12 = 43200.</summary>
    public const int MinGmtOffsetSeconds = -50400;
    public const int MaxGmtOffsetSeconds = 43200;

    /// <summary>Spec §6.3: 60,000 events (HID recommends at least 60,000), 100,000 cards, 32 access levels per card, UTC+3.</summary>
    public static readonly ScpAllocation Default = new(Transactions: 60000, Cards: 100000, AccessLevelsPerCard: 32, GmtOffsetSeconds: -10800);

    public static bool IsValidGmtOffset(int gmtOffsetSeconds) =>
        gmtOffsetSeconds is >= MinGmtOffsetSeconds and <= MaxGmtOffsetSeconds && gmtOffsetSeconds % 900 == 0;

    /// <summary>UTC offset in hours for display (the SDK stores the opposite sign, in seconds).</summary>
    public double UtcOffsetHours => -GmtOffsetSeconds / 3600d;

    public static int GmtOffsetSecondsFromUtcHours(double utcOffsetHours) => (int)Math.Round(-utcOffsetHours * 3600);
}
