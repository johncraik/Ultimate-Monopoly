namespace UltimateMonopoly.Areas.Admin.Enums;

/// <summary>The status filter on the per-user notifications page — each maps to a distinct
/// <c>NotificationService</c> call (active → GetNotifications/OnlyActive, dismissed → GetNotifications/OnlyDeleted,
/// expired → GetExpiredNotifications).</summary>
public enum NotificationStatusFilter
{
    Active,
    Dismissed,
    Expired
}
