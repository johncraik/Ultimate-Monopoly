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
    private IQueryable<AuditEntry> Query(string? userId, string? search, AuditAction? action)
    {
        var query = _context.AuditEntries.AsNoTracking().AsQueryable();

        if (!string.IsNullOrEmpty(userId))
            query = query.Where(a => a.UserId == userId);

        if (action.HasValue)
            query = query.Where(a => a.Action == action.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(a => a.TableName != null && a.TableName.ToLower().Contains(search));
        }

        return query.OrderByDescending(a => a.AuditDate);
    }

    /// <summary>A single user's audit trail — every action they performed across every table (§9.3).</summary>
    public async Task<PagedList<AuditEntryViewModel>> GetUserTrail(string userId, int pageNumber, int pageSize,
        string? search, AuditAction? action)
    {
        var paged = await Query(userId, search, action).ToPagedListAsync(pageNumber, pageSize);
        var entries = paged.Select(e => new AuditEntryViewModel(e)).ToList();
        return new PagedList<AuditEntryViewModel>(entries, paged.PageNumber, paged.PageSize, paged.TotalCount);
    }
}