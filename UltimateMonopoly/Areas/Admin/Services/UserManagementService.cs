using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Pagination;
using JC.Identity.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Middleware;
using UltimateMonopoly.Areas.Admin.Models.ViewModels.Users;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Areas.Admin.Services;

public class UserManagementService
{
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly AdminLogService _adminLog;
    private readonly AuthRefreshService _authRefreshService;
    private readonly IUserInfo _userInfo;

    public UserManagementService(AppDbContext context,
        UserManager<AppUser> userManager,
        AdminLogService adminLog,
        AuthRefreshService authRefreshService,
        IUserInfo userInfo)
    {
        _context = context;
        _userManager = userManager;
        _adminLog = adminLog;
        _authRefreshService = authRefreshService;
        _userInfo = userInfo;
        if (!userInfo.IsInRole(SystemRoles.Admin)
            && !userInfo.IsInRole(SystemRoles.SystemAdmin)
            && !userInfo.IsInRole(AppRoles.GithubManager))
            throw new UnauthorizedAccessException(
                "You are not authorized to perform this action."
            );
    }

    private IQueryable<AppUser> QueryUsers(string? search, bool asNoTracking, bool? enabled)
    {
        var query = _context.Users.AsQueryable();
        if (asNoTracking)
            query = query.AsNoTracking();
        
        if(enabled.HasValue)
            query = query.Where(u => u.IsEnabled == enabled.Value);

        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.ToLower();
            query = query.Where(u => (!string.IsNullOrEmpty(u.UserName) && u.UserName.ToLower().Contains(search))
                                     || (!string.IsNullOrEmpty(u.DisplayName) && u.DisplayName.ToLower().Contains(search))
                                     || (!string.IsNullOrEmpty(u.Email) && u.Email.ToLower().Contains(search))
                                     || (!string.IsNullOrEmpty(u.PhoneNumber) && u.PhoneNumber.ToLower().Contains(search)));
        }
        
        return query;
    }

    private async Task<HashSet<string>> RestrictedUserIds(params IEnumerable<AppUser> users)
    {
        var list = users.ToList();
        var restrictedUserIds = new HashSet<string>();
        foreach (var user in list)
        {
            var inRole = await _userManager.IsInRoleAsync(user, AppRoles.Restricted);
            if (inRole)
                restrictedUserIds.Add(user.Id);
        }
        
        return restrictedUserIds;
    }

    private async Task<List<string>> UserRoles(AppUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);
        return roles.ToList();
    }


    public async Task<PagedList<UserViewModel>> GetUsers(int pageNumber, int pageSize, string? search, UserManagementFilter filter)
    {
        bool? enabled = null;
        if(filter.HasFlag(UserManagementFilter.Disabled))
            enabled = false;
        else if(filter.HasFlag(UserManagementFilter.Enabled))
            enabled = true;

        var users = await QueryUsers(search, true, enabled)
            .ToListAsync();
        
        bool? restricted = null;
        if(filter.HasFlag(UserManagementFilter.Restricted))
            restricted = true;
        else if(filter.HasFlag(UserManagementFilter.NotRestricted))
            restricted = false;
        
        var restrictedUserIds = await RestrictedUserIds(users);
        return users.Select(u => new UserViewModel(u, restrictedUserIds.Contains(u.Id)))
            .Where(u => restricted == null || u.IsRestricted == restricted)
            .OrderBy(u => u.Profile.Username)
            .ToPagedList(pageNumber, pageSize); //Paginated in memory since restricted users are not filtered in user query
    }

    
    public async Task<UserViewModel?> GetUserById(string userId)
    {
        var user = await QueryUsers(null, true, null)
            .FirstOrDefaultAsync(u => u.Id == userId);
        if(user == null) return null;
        
        var roles = await UserRoles(user);
        return new UserViewModel(user, roles.Contains(AppRoles.Restricted), roles);
    }

    // ---- Actions (each mutates then writes an AdminActionLog; returns false on a missing user / failed op) ----

    private void RefreshUserSignIn(string userId)
    {
        _authRefreshService.RefreshUserSignIn(userId);
    }
    
    
    /// <summary>Adds or removes a role, no-op if already in the desired state. Returns whether the user ends in that state.</summary>
    private async Task<bool> ApplyRole(AppUser user, string role, bool grant)
    {
        var inRole = await _userManager.IsInRoleAsync(user, role);
        if (inRole == grant) return true;
        
        //Check Admin is NOT assigning SystemAdmin/Admin (other roles allowed)
        if(!_userInfo.IsInRole(SystemRoles.SystemAdmin) 
           && (string.Equals(role, SystemRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase)
           || string.Equals(role, SystemRoles.Admin, StringComparison.OrdinalIgnoreCase)))
            return false;
        
        var result = grant
            ? await _userManager.AddToRoleAsync(user, role)
            : await _userManager.RemoveFromRoleAsync(user, role);
        return result.Succeeded;
    }

    public async Task<bool> ChangeDisplayName(string userId, string? newDisplayName)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        var old = user.DisplayName;
        newDisplayName = string.IsNullOrWhiteSpace(newDisplayName) ? null : newDisplayName.Trim();
        if (old == newDisplayName) return true;

        user.DisplayName = newDisplayName;
        if (!(await _userManager.UpdateAsync(user)).Succeeded) return false;

        await _adminLog.LogDisplayNameChange(userId, old ?? "(none)", newDisplayName ?? "(none)");
        RefreshUserSignIn(userId);
        return true;
    }

    public async Task<bool> SetHidden(string userId, bool hidden)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !await ApplyRole(user, AppRoles.HiddenUser, hidden)) return false;

        if (hidden) await _adminLog.LogUserHidden(userId);
        else await _adminLog.LogUserShown(userId);
        
        RefreshUserSignIn(userId);
        return true;
    }

    public async Task<bool> SetRestricted(string userId, bool restricted)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;

        //Admin cannot restrict SystemAdmin
        if(await _userManager.IsInRoleAsync(user, SystemRoles.SystemAdmin) 
           && !_userInfo.IsInRole(SystemRoles.SystemAdmin))
            return false;
        
        var result = await ApplyRole(user, AppRoles.Restricted, restricted);
        if(!result) return false;

        if (restricted) await _adminLog.LogUserRestricted(userId);
        else await _adminLog.LogUserUnrestricted(userId);
        
        RefreshUserSignIn(userId);
        return true;
    }

    public async Task<bool> SetEnabled(string userId, bool enabled, bool idempotent = true)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        if (user.IsEnabled == enabled) return idempotent;

        //Admin cannot disable SystemAdmin
        if(await _userManager.IsInRoleAsync(user, SystemRoles.SystemAdmin) 
           && !_userInfo.IsInRole(SystemRoles.SystemAdmin))
            return false;
        
        user.IsEnabled = enabled;
        if (!(await _userManager.UpdateAsync(user)).Succeeded) return false;

        if (enabled) await _adminLog.LogUserEnabled(userId);
        else await _adminLog.LogUserDisabled(userId);
        
        RefreshUserSignIn(userId);
        return true;
    }

    /// <summary>Grants/removes Admin or SystemAdmin. Caller must gate this to SystemAdmin (design §4).</summary>
    public async Task<bool> SetRole(string userId, string role, bool grant)
    {
        //System Admin Only
        if(!_userInfo.IsInRole(SystemRoles.SystemAdmin))
            return false;
        
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null || !await ApplyRole(user, role, grant)) return false;

        if (grant) await _adminLog.LogRoleAdded(userId, role);
        else await _adminLog.LogRoleRemoved(userId, role);
        
        RefreshUserSignIn(userId);
        return true;
    }

    /// <summary>
    /// Hard-deletes the account. Stricter than the matrix: a SystemAdmin can't be deleted (remove the
    /// role first), and nobody can delete their own account. Caller must also gate this to SystemAdmin.
    /// Records remain orphaned-by-id until the cleanup job runs (deferred — design §6.3).
    /// </summary>
    public async Task<bool> DeleteUser(string userId)
    {
        //Cant delete system admin, nor delete if not system admin
        if (userId == _userInfo.UserId 
            || !_userInfo.IsInRole(SystemRoles.SystemAdmin)) return false;

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null) return false;
        if (await _userManager.IsInRoleAsync(user, SystemRoles.SystemAdmin)) return false;

        if (!(await _userManager.DeleteAsync(user)).Succeeded) return false;

        await _adminLog.LogUserDeleted(userId);
        return true;
    }
}