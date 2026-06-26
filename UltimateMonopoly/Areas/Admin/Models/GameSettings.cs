namespace UltimateMonopoly.Areas.Admin.Models;

public class GameSettings
{
    /// <summary>Whether soft deleted game records are permanently deleted</summary>
    public bool EnableCleanup { get; set; } = true;
    /// <summary>How many months to keep soft deleted records before they are permanently deleted (null when cleanup is disabled)</summary>
    public int? CleanupRetentionMonths { get; set; } = 3;

    /// <summary>Whether abandoned games are managed via background jobs</summary>
    public bool EnableAbandonedGamesManagement { get; set; } = true;
    /// <summary>Action to take when an abandoned game is detected</summary>
    public AbandonedGameAction AbandonedGameAction { get; set; } = AbandonedGameAction.Cancel;
    /// <summary>Number of weeks before a game is considered abandoned (with no new turn records created)</summary>
    public int AbandonedRetentionWeeks { get; set; } = 6;

    /// <summary>Whether cancelled games are soft deleted after a period of time</summary>
    public bool EnableAutoDeleteCancelled { get; set; } = true;
    /// <summary>Number of months before a canclled game is soft deleted (null when auto delete cancelled is disabled)</summary>
    public int? AutoDeleteCancelledRetentionMonths { get; set; } = 3;

    /// <summary>Whether snapshots (and events) are soft deleted after a period of time for finished or cancelled games</summary>
    public bool EnableAutoDeleteSnapshots { get; set; } = false;
    /// <summary>Number of months before a snapshot (and event) is soft deleted (null when auto delete snapshots is disabled)</summary>
    public int? AutoDeleteSnapshotsRetentionMonths { get; set; } = null;
}

public enum AbandonedGameAction
{
    Draw,
    Cancel
}