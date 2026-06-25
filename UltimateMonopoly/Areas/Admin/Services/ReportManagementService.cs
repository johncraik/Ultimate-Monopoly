using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Pagination;
using JC.Core.Services.DataRepositories;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Reports;
using UltimateMonopoly.Models.DataModels.Social;

namespace UltimateMonopoly.Areas.Admin.Services;

public class ReportManagementService
{
    private readonly IRepositoryManager _repos;
    private readonly UserManagementService _userManagementService;
    private readonly AdminLogService _adminLogService;

    public ReportManagementService(IRepositoryManager repos,
        UserManagementService userManagementService,
        AdminLogService adminLogService,
        IUserInfo userInfo)
    {
        _repos = repos;
        _userManagementService = userManagementService;
        _adminLogService = adminLogService;
        
        if(!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("User is not authorized to access this resource.");
    }

    private IQueryable<ReportedUser> QueryReports(string? search, bool asNoTracking, ReportResolution resolution)
    {
        var query = _repos.GetRepository<ReportedUser>()
            .AsQueryable();

        if (asNoTracking)
            query = query.AsNoTracking();

        query = resolution switch
        {
            ReportResolution.Open => query.Where(r => r.Resolution == ReportResolution.Open),
            ReportResolution.FullAction => query.Where(r => r.Resolution == ReportResolution.FullAction),
            ReportResolution.AllActions => query.Where(r => r.Resolution == ReportResolution.AllActions),
            ReportResolution.AnyAction => query,
            ReportResolution.Handled => query.Where(r => r.Resolution != ReportResolution.Open),
            _ => query.Where(r => r.Resolution == resolution)
        };

        if (!string.IsNullOrEmpty(search))
        {
            search = search.ToLower();
            query = query.Where(r => (!string.IsNullOrEmpty(r.Message) && r.Message.ToLower().Contains(search)) 
                                     || r.BlockedUser.BlockedUserId == search);
        }

        return query.Include(r => r.BlockedUser)
            .OrderBy(r => r.Resolution)
            .ThenByDescending(r => r.RestoredUtc);
    }

    public async Task<PagedList<ReportViewModel>> GetReports(int pageNumber, int pageSize, string? search, ReportResolution resolution)
    {
        var reports = await QueryReports(search, true, resolution)
            .ToListAsync();

        if (!string.IsNullOrEmpty(search))
            reports = reports.Where(r =>
            {
                var reason = r.Reason.ToDisplayName();
                return reason.ToLower().Contains(search) || search.ToLower().Contains(reason);
            }).ToList();
        
        var viewModels = new List<ReportViewModel>();
        foreach (var r in reports)
        {
            var reportedUser = await _userManagementService.GetUserById(r.BlockedUser.BlockedUserId);
            var reportedByUser = await _userManagementService.GetUserById(r.BlockedUser.FromUserId);
            
            viewModels.Add(new ReportViewModel(reportedUser, reportedByUser, r));
        }

        return viewModels.ToPagedList(pageNumber, pageSize);
    }

    public async Task<ReportViewModel?> GetReportById(string reportId)
    {
        var report = await QueryReports(null, false, ReportResolution.AnyAction)
            .FirstOrDefaultAsync(r => r.BlockedId == reportId);
        if(report == null) return null;
        
        var reportedUser = await _userManagementService.GetUserById(report.BlockedUser.BlockedUserId);
        var reportedByUser = await _userManagementService.GetUserById(report.BlockedUser.FromUserId);
        
        return new ReportViewModel(reportedUser, reportedByUser, report);
    }



    public async Task<bool> TryHandleReport(string reportId)
    {
        //Can only "handle" open reports:
        var report = await QueryReports(null, false, ReportResolution.AnyAction)
            .FirstOrDefaultAsync(r => r.BlockedId == reportId && r.Resolution == ReportResolution.Open);
        if (report == null) return false;
        
        //"Handled" is a no-op resolve for report (no action, but not open)
        var oldResolution = report.Resolution;
        report.Resolution = ReportResolution.Handled;
        await _repos.GetRepository<ReportedUser>()
            .UpdateAsync(report);
        
        await _adminLogService.LogReportHandled(report.BlockedId, oldResolution, ReportResolution.Handled);
        return true;
    }

    public async Task<bool> TryRestrictUser(string reportId)
    {
        //Can only restrict user where report has not been marked for restricted, fullAction, delete, or all
        var report = await QueryReports(null, false, ReportResolution.AnyAction)
            .FirstOrDefaultAsync(r => r.BlockedId == reportId 
                                      && (r.Resolution == ReportResolution.Open 
                                          || r.Resolution == ReportResolution.AccountDisabled 
                                          || r.Resolution == ReportResolution.Handled));
        if (report == null) return false;
        
        var result = await _userManagementService.SetRestricted(report.BlockedUser.BlockedUserId, true);
        if (!result) return false;
        
        var oldResolution = report.Resolution;
        if (report.Resolution == ReportResolution.Handled) 
            report.Resolution = ReportResolution.AccountRestricted;
        else report.Resolution |= ReportResolution.AccountRestricted;
        
        await _repos.GetRepository<ReportedUser>()
            .UpdateAsync(report);
        
        await _adminLogService.LogReportHandled(report.BlockedId, oldResolution, report.Resolution);
        return true;
    }

    public async Task<bool> TryDisableUser(string reportId)
    {
        //Can only disable user where report has not been marked for disabled, fullAction, delete, or all
        var report = await QueryReports(null, false, ReportResolution.AnyAction)
            .FirstOrDefaultAsync(r => r.BlockedId == reportId 
                                      && (r.Resolution == ReportResolution.Open 
                                          || r.Resolution == ReportResolution.AccountRestricted 
                                          || r.Resolution == ReportResolution.Handled));
        if (report == null) return false;
        
        //returns false if already disabled - prevents admin log (cant disable already disabled account)
        var result = await _userManagementService.SetEnabled(report.BlockedUser.BlockedUserId, false, false);
        if (!result) return false;
        
        var oldResolution = report.Resolution;
        if (report.Resolution == ReportResolution.Handled) 
            report.Resolution = ReportResolution.AccountDisabled;
        else report.Resolution |= ReportResolution.AccountDisabled;
        
        await _repos.GetRepository<ReportedUser>()
            .UpdateAsync(report);
        
        await _adminLogService.LogReportHandled(report.BlockedId, oldResolution, report.Resolution);
        return true;
    }

    public async Task<bool> TryDeleteUser(string reportId)
    {
        //Can only disable user where report has not been marked for disabled, fullAction, delete, or all
        var report = await QueryReports(null, false, ReportResolution.AnyAction)
            .FirstOrDefaultAsync(r => r.BlockedId == reportId 
                                      && r.Resolution != ReportResolution.AccountDeleted 
                                          && r.Resolution != ReportResolution.AllActions);
        if (report == null) return false;
        
        var result = await _userManagementService.DeleteUser(report.BlockedUser.BlockedUserId);
        if (!result) return false;
        
        var oldResolution = report.Resolution;
        if (report.Resolution == ReportResolution.Handled) 
            report.Resolution = ReportResolution.AccountDeleted;
        else report.Resolution |= ReportResolution.AccountDeleted;
        
        await _repos.GetRepository<ReportedUser>()
            .UpdateAsync(report);
        
        await _adminLogService.LogReportHandled(report.BlockedId, oldResolution, report.Resolution);
        return true;
    }
}