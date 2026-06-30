namespace UltimateMonopoly.Areas.Admin.Enums;

/// <summary>What an <c>AdminActionLog</c> entry was performed against (C1 — see design-docs/c1-admin-area.md §5).</summary>
public enum AdminTargetType
{
    User,
    Game,
    Report,
    Role,
    Config,
    Issue
}
