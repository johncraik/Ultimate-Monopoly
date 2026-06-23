namespace UltimateMonopoly.Areas.Admin.Enums;

/// <summary>The kind of admin action recorded in an <c>AdminActionLog</c> (C1 — see design-docs/c1-admin-area.md §5).</summary>
public enum AdminActionType
{
    // Users
    RoleAdded,
    RoleRemoved,
    UserDisabled,
    UserEnabled,
    UserDeleted,
    UserDisplayNameUpdated,
    UserHidden,
    UserShown,

    // Reports
    ReportResolved,

    // Games
    GameDrawn,
    GameCancelled,
    GameDeleted,

    // Config
    RulesUpdated,
    TurnTaxUpdated,
    SettingsUpdated
}
