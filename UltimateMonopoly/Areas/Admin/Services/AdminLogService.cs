using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Identity.Authentication;
using UltimateMonopoly.Areas.Admin.Enums;
using UltimateMonopoly.Areas.Admin.Models;

namespace UltimateMonopoly.Areas.Admin.Services;

public class AdminLogService
{
    private readonly IRepositoryContext<AdminActionLog> _logs;
    private readonly IUserInfo _userInfo;

    public AdminLogService(IRepositoryContext<AdminActionLog> logs,
        IUserInfo userInfo)
    {
        _logs = logs;
        _userInfo = userInfo;
        if(!userInfo.IsInRole(SystemRoles.Admin) && !userInfo.IsInRole(SystemRoles.SystemAdmin))
            throw new UnauthorizedAccessException(
                "You are not authorized to perform this action."
            );
    }

    private string AdminLogIdentifier => $"{_userInfo.Username} ({_userInfo.UserId})";
    
    public async Task LogDisplayNameChange(string userId, string oldDisplayName, string newDisplayName)
    {
        var detail = $"{AdminLogIdentifier} changed display name for user '{userId}' from {oldDisplayName} to {newDisplayName}.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.UserDisplayNameUpdated,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogUserHidden(string userId)
    {
        var detail = $"{AdminLogIdentifier} hidden user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.UserHidden,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogUserShown(string userId)
    {
        var detail = $"{AdminLogIdentifier} shown user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.UserShown,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });  
    }

    public async Task LogUserEnabled(string userId)
    {
        var detail = $"{AdminLogIdentifier} enabled user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.UserEnabled,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });   
    }
    
    public async Task LogUserDisabled(string userId)
    {
        var detail = $"{AdminLogIdentifier} disabled user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.UserDisabled,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogUserRestricted(string userId)
    {
        var detail = $"{AdminLogIdentifier} restricted user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.RoleAdded,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogUserUnrestricted(string userId)
    {
        var detail = $"{AdminLogIdentifier} removed the restriction on user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.RoleRemoved,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogRoleAdded(string userId, string role)
    {
        var detail = $"{AdminLogIdentifier} granted the {role} role to user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.RoleAdded,
            TargetType = AdminTargetType.Role,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogRoleRemoved(string userId, string role)
    {
        var detail = $"{AdminLogIdentifier} removed the {role} role from user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.RoleRemoved,
            TargetType = AdminTargetType.Role,
            TargetId = userId,
            Detail = detail
        });
    }

    public async Task LogUserDeleted(string userId)
    {
        var detail = $"{AdminLogIdentifier} deleted user '{userId}'.";
        await SaveLog(new AdminActionLog
        {
            Action = AdminActionType.UserDeleted,
            TargetType = AdminTargetType.User,
            TargetId = userId,
            Detail = detail
        });
    }



    private async Task SaveLog(AdminActionLog log)
    {
        await _logs.AddAsync(log);
    }
}