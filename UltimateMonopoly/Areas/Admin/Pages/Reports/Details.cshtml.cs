using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Reports;
using UltimateMonopoly.Areas.Admin.Services;

namespace UltimateMonopoly.Areas.Admin.Pages.Reports;

public class DetailsModel : PageModel
{
    private readonly ReportManagementService _reports;
    private readonly RecentActivityService _activity;
    private readonly IUserInfo _userInfo;

    public DetailsModel(ReportManagementService reports, RecentActivityService activity, IUserInfo userInfo)
    {
        _reports = reports;
        _activity = activity;
        _userInfo = userInfo;
    }

    public string ReportId { get; set; } = "";

    public ReportViewModel Report { get; private set; } = default!;

    // The reported user's recent-activity panel (§7.3) — keyed by ReportedUserId, so it surfaces even when
    // the account has since been deleted (its historical logs/audit/games persist under the id).
    public RecentActivityModel Activity { get; private set; } = default!;

    // Open vs resolved (resolved = any non-open resolution).
    public bool IsOpen => Report.Resolution == ReportResolution.Open;

    // ---- View gates (mirror the ReportManagementService / UserManagementService server-side guards) ----
    public bool IsSystemAdmin => _userInfo.IsInRole(SystemRoles.SystemAdmin);
    public bool UserExists => Report.ReportedUser != null;
    public bool IsSelf => Report.ReportedUserId == _userInfo.UserId;
    public bool TargetIsSystemAdmin => Report.ReportedUser?.Roles.Contains(SystemRoles.SystemAdmin) ?? false;
    // A plain Admin can't moderate a SystemAdmin (mirrors UserManagementService).
    public bool CanModerateTarget => IsSystemAdmin || !TargetIsSystemAdmin;
    // Gates the activity panel's admin-logs stream (§7.4) — the reported user currently holds an admin role.
    public bool ReportedUserHoldsAdminRole =>
        (Report.ReportedUser?.Roles.Contains(SystemRoles.Admin) ?? false) || TargetIsSystemAdmin;

    // Resolution-state gates — match the allowed-state checks in each Try* method.
    public bool CanHandle => Report.Resolution == ReportResolution.Open;
    public bool CanRestrict => UserExists && CanModerateTarget &&
        (Report.Resolution == ReportResolution.Open
         || Report.Resolution == ReportResolution.AccountDisabled
         || Report.Resolution == ReportResolution.Handled);
    public bool CanDisable => UserExists && CanModerateTarget &&
        (Report.Resolution == ReportResolution.Open
         || Report.Resolution == ReportResolution.AccountRestricted
         || Report.Resolution == ReportResolution.Handled);
    public bool CanDelete => IsSystemAdmin && UserExists && CanModerateTarget && !IsSelf
        && Report.Resolution != ReportResolution.AccountDeleted
        && Report.Resolution != ReportResolution.AllActions;

    public async Task<IActionResult> OnGetAsync(string reportId)
    {
        ReportId = reportId;
        
        var report = await _reports.GetReportById(ReportId);
        if (report == null) return NotFound();

        Report = report;
        Activity = await _activity.Build(report.ReportedUserId, IsSystemAdmin, ReportedUserHoldsAdminRole);
        return Page();
    }

    // The Try* methods are authoritative (resolution-state + tier/peer guards); the UI gates are
    // defence-in-depth. Each redirects back with a TempData message.

    public async Task<IActionResult> OnPostHandleAsync(string reportId)
    {
        ReportId = reportId;
        
        if (await _reports.TryHandleReport(ReportId)) TempData["Success"] = "Report marked as handled.";
        else TempData["Error"] = "Couldn't mark this report as handled — only an open report can be handled.";
        return RedirectToPage(new { reportId = ReportId });
    }

    public async Task<IActionResult> OnPostRestrictAsync(string reportId)
    {
        ReportId = reportId;
        
        if (await _reports.TryRestrictUser(ReportId)) TempData["Success"] = "Reported user restricted.";
        else TempData["Error"] = "Couldn't restrict this user — the report may already be restricted, or you can't moderate this account.";
        return RedirectToPage(new { reportId = ReportId });
    }

    public async Task<IActionResult> OnPostDisableAsync(string reportId)
    {
        ReportId = reportId;
        
        if (await _reports.TryDisableUser(ReportId)) TempData["Success"] = "Reported user disabled.";
        else TempData["Error"] = "Couldn't disable this user — the account may already be disabled, or you can't moderate this account.";
        return RedirectToPage(new { reportId = ReportId });
    }

    public async Task<IActionResult> OnPostDeleteAsync(string reportId)
    {
        ReportId = reportId;
        
        if (!IsSystemAdmin) return Forbid();

        if (await _reports.TryDeleteUser(ReportId)) TempData["Success"] = "Reported user deleted.";
        else TempData["Error"] = "Couldn't delete this user — a SystemAdmin must have their role removed first, and you can't delete your own account.";
        return RedirectToPage(new { reportId = ReportId });
    }
}