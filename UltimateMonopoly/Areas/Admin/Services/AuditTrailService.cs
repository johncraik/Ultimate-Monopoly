using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Auditing;
using JC.Core.Models.Pagination;
using JC.Identity.Authentication;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Audits;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Services;

/// <summary>
/// Read-only access to the JC.Core audit trail (<see cref="AuditEntry"/>) for the admin Audit area.
/// JC.Core ships no viewer — it only persists entries and prunes them (<c>AuditCleanupJob</c>) — so the
/// queries live here. Admin- or SystemAdmin-gated (audit is read-only moderation, both tiers).
/// </summary>
public class AuditTrailService
{
    private readonly AppDbContext _context;

    public AuditTrailService(AppDbContext context, IUserInfo userInfo)
    {
        _context = context;
        if (!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException("You are not authorized to perform this action.");
    }

    /// <summary>Audit entries newest-first, optionally scoped to one actor and filtered by table-name search + action.</summary>
    private IQueryable<AuditEntry> Query(string? userId, bool system, string? search, AuditAction? action, string? tableName)
    {
        var query = _context.AuditEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(a => a.UserId == userId);
        else if (system)
            query = query.Where(a => a.UserId == IUserInfo.UNKNOWN_USER_ID || a.UserId == IUserInfo.SYSTEM_USER_ID);
        
        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);

        if(!string.IsNullOrEmpty(tableName))
            query = query.Where(a => a.TableName == tableName);
        
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = !string.IsNullOrEmpty(userId)
                //Filter search to be TableView (search userId and userName)
                ? query.Where(a => (!string.IsNullOrEmpty(a.EntityKey) && a.EntityKey.ToLower().Contains(search)) 
                                   || (a.TableName != null && a.TableName.ToLower().Contains(search)))
                //Filter search to be UserView (search tableName)
                : query.Where(a => (!string.IsNullOrEmpty(a.EntityKey) && a.EntityKey.ToLower().Contains(search)) 
                                   || (!string.IsNullOrEmpty(a.UserId) && a.UserId.ToLower().Contains(search)) 
                                   || (!string.IsNullOrEmpty(a.UserName) && a.UserName.ToLower().Contains(search)));
        }

        return query.OrderByDescending(a => a.AuditDate);
    }

    /// <summary>A single user's audit trail — every action they performed across every table (§9.3).</summary>
    public async Task<PagedList<AuditEntryViewModel>> GetUserTrail(string userId, int pageNumber, int pageSize,
        string? search, AuditAction? action)
    {
        var paged = await Query(userId, false, search, action, null).ToPagedListAsync(pageNumber, pageSize);
        var entries = paged.Select(e => new AuditEntryViewModel(e, true)).ToList();
        return new PagedList<AuditEntryViewModel>(entries, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }
    
    public async Task<PagedList<AuditEntryViewModel>> GetSystemTrail(int pageNumber, int pageSize,
        string? search, AuditAction? action)
    {
        var paged = await Query(null, true, search, action, null).ToPagedListAsync(pageNumber, pageSize);
        var entries = paged.Select(e => new AuditEntryViewModel(e, true)).ToList();
        return new PagedList<AuditEntryViewModel>(entries, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }
    
    public async Task<PagedList<AuditEntryViewModel>> GetDataTableTrail(string tableName, int pageNumber, int pageSize,
        string? search, AuditAction? action)
    {
        var paged = await Query(null, false, search, action, tableName).ToPagedListAsync(pageNumber, pageSize);
        var entries = paged.Select(e => new AuditEntryViewModel(e, false)).ToList();
        return new PagedList<AuditEntryViewModel>(entries, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }

    public async Task<List<AuditDataTableViewModel>> GetAuditTableNames()
    {
        var tables = await _context.AuditEntries
            .GroupBy(a => a.TableName)
            .Select(g => new
            {
                TableName = g.Key,
                Count = g.Count(),
                Latest = g.Max(a => a.AuditDate)
            })
            .ToListAsync();

        return tables.Where(t => !string.IsNullOrEmpty(t.TableName))
            .Select(t => new AuditDataTableViewModel(t.TableName!, t.Count, t.Latest.ToLocalTime().ToString("g")))
            .ToList();
    }
}