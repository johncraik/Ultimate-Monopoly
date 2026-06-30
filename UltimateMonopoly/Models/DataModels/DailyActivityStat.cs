using System.ComponentModel.DataAnnotations;

namespace UltimateMonopoly.Models.DataModels;

/// <summary>
/// One day's user-activity snapshot — captured daily by <c>DailyStatsJob</c> so the admin dashboard can show
/// trends the live tables can't reconstruct (we store only the <i>latest</i> <c>LastLoginUtc</c>/<c>LastActiveUtc</c>,
/// not history). Registration-trend is derived directly from <c>AppUser.RegisteredUtc</c> and doesn't need this;
/// logins-over-time and DAU/MAU history accrue here going forward.
/// </summary>
public class DailyActivityStat
{
    /// <summary>The (UTC) day this snapshot covers, at 00:00.</summary>
    [Key]
    public DateTime Date { get; set; }

    public int TotalUsers { get; set; }
    public int NewUsers { get; set; }  // registered on this day
    public int Logins { get; set; }    // users whose latest login fell on this day
    public int Dau { get; set; }       // active on this day
    public int Wau { get; set; }       // active in the trailing 7 days
    public int Mau { get; set; }       // active in the trailing 30 days
}