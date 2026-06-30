using System.ComponentModel.DataAnnotations;
using JC.Core.Models.Auditing;
using UltimateMonopoly.Areas.Admin.Enums;

namespace UltimateMonopoly.Areas.Admin.Models;

/// <summary>
/// An immutable record of a single admin action (C1 — design-docs/c1-admin-area.md §5). Fills the gap
/// the JC.Core audit trail leaves: role / account changes go through Identity (not <c>AuditModel</c>)
/// and rules / turn-tax / settings are files, so none of them are audited automatically. Every mutating
/// admin handler writes one of these as its last step. <c>LogModel</c> ⇒ append-only: the acting admin
/// and timestamp populate from <c>IUserInfo</c> (<c>CreatedById</c> / <c>CreatedUtc</c>), and update /
/// soft-delete / restore are forbidden.
/// </summary>
public class AdminActionLog : LogModel
{
    [Key]
    [MaxLength(38)]
    public string Id { get; private set; } = Guid.NewGuid().ToString();

    /// <summary>What was done.</summary>
    public AdminActionType Action { get; set; }

    /// <summary>What it was done against.</summary>
    public AdminTargetType TargetType { get; set; }

    /// <summary>The target's id — a user / game / report id, or a config name. Null for area-wide actions.</summary>
    [MaxLength(38)]
    public string? TargetId { get; set; }

    /// <summary>A human-readable summary (or small JSON) of the change — e.g. "added Restricted", "retention → 6 months".</summary>
    [MaxLength(2048)]
    public string? Detail { get; set; }

    public AdminActionLog()
    {
    }

    public AdminActionLog(AdminActionType action, AdminTargetType targetType, string? targetId = null, string? detail = null)
    {
        Action = action;
        TargetType = targetType;
        TargetId = targetId;
        Detail = detail;
    }
}
