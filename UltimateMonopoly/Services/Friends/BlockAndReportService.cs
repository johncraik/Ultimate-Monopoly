using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Pagination;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Models.DataModels.Social;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Services.Friends;

public class BlockAndReportService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly UrlLinkService _urlLinkService;
    private readonly ILogger<BlockAndReportService> _logger;
    private readonly UserService _userService;

    public BlockAndReportService(IRepositoryManager repos,
        IUserInfo userInfo,
        UrlLinkService urlLinkService,
        ILogger<BlockAndReportService> logger,
        UserService userService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _urlLinkService = urlLinkService;
        _logger = logger;
        _userService = userService;
    }

    public async Task<PagedList<UserProfileViewModel>> GetBlockedUsers(ushort pageNumber, ushort pageSize)
    {
        var blockedUsers = await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(b => b.CreatedById == _userInfo.UserId)
            .ToListAsync();
        
        var userIds = blockedUsers.Select(b => b.BlockedUserId).Distinct().ToList();
        var users = await _userService.GetUserDictionary(userIds);

        var blockedViewModels = new List<UserProfileViewModel>();
        foreach (var bu in blockedUsers)
        {
            if(!users.TryGetValue(bu.BlockedUserId, out var user))
                continue;
            
            var imgUrl = _urlLinkService.GetImgUrl(user.AvatarImageName);
            blockedViewModels.Add(new UserProfileViewModel(user, imgUrl));
        }
        return blockedViewModels.OrderBy(bu => bu.DisplayName)
            .ToPagedList(pageNumber, pageSize);
    }

    public async Task<bool> CheckIfBlocksExist(string userId, IEnumerable<string> conflictingUserIds)
    {
        var list = conflictingUserIds.ToList();
        return await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => (b.CreatedById == userId && list.Contains(b.BlockedUserId)) 
                           || (list.Contains(b.CreatedById!) && b.BlockedUserId == userId));
    }

    public async Task<List<(bool Blocked, string userId)>> CheckAndReportExistingBlocks(string userId, IEnumerable<string> conflictingUserIds)
    {
        var list = conflictingUserIds.ToList();
        var blocked = await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(b => (b.CreatedById == userId && list.Contains(b.BlockedUserId)
                         || list.Contains(b.CreatedById!) && b.BlockedUserId == userId))
            .Select(b => new
            {
                UserId = b.CreatedById == userId ? b.BlockedUserId : b.CreatedById
            })
            .ToListAsync();
        return list.Select(u => (blocked.Any(b => b.UserId == u), u)).ToList();
    }
    
    
    private async Task<BlockedUser?> GetBlockedUser(string userId) 
        => await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(b => b.CreatedById == _userInfo.UserId && b.BlockedUserId == userId);

    private async Task<bool> CheckUserId(string userId)
        => userId != _userInfo.UserId && await _userService.ValidUser(userId);
    
    public async Task<bool> TryBlockUser(string userId)
    {
        var result = await CheckUserId(userId);
        if (!result) return false;
        
        var blockedUser = await GetBlockedUser(userId);
        if (blockedUser != null) return true;
        
        blockedUser = new BlockedUser(userId);
        return await ProcessBlockAndReport(userId, blockedUser, null);
    }

    public async Task<bool> TryBlockAndReport(string userId, ReportInput report)
    {
        var result = await CheckUserId(userId);
        if (!result) return false;
        
        var blockedUser = await GetBlockedUser(userId);
        var blockedExists = blockedUser != null;
        blockedUser ??= new BlockedUser(userId);

        var reportedUser = new ReportedUser(blockedUser.Id, report);
        return await ProcessBlockAndReport(userId, blockedExists ? null : blockedUser, reportedUser);
    }

    private async Task<bool> ProcessBlockAndReport(string userId, BlockedUser? blockedUser, ReportedUser? reportedUser)
    {
        if(reportedUser == null && blockedUser == null)
            return true;

        Friend? friend = null;
        List<FriendRequest> pendingRequests = [];
        if (blockedUser != null)
        {
            friend = await _repos.GetRepository<Friend>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
                .FirstOrDefaultAsync(f => (f.CreatedById == _userInfo.UserId && f.FriendUserId == userId)
                                          || (f.FriendUserId == _userInfo.UserId && f.CreatedById == userId));
            friend?.Remove();

            // A block clears any still-pending friend request between the two users, both
            // directions — a block must leave no live request behind to accept later.
            pendingRequests = await _repos.GetRepository<FriendRequest>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
                .Where(r => r.IsAccepted == null
                            && ((r.CreatedById == _userInfo.UserId && r.ToUserId == userId)
                                || (r.CreatedById == userId && r.ToUserId == _userInfo.UserId)))
                .ToListAsync();
        }
        
        await _repos.BeginTransactionAsync();
        try
        {
            if(reportedUser != null)
                await _repos.GetRepository<ReportedUser>()
                    .AddAsync(reportedUser, saveNow: false);
            
            if(blockedUser != null)
                await _repos.GetRepository<BlockedUser>()
                    .AddAsync(blockedUser, saveNow: false);
            
            if(friend != null)
                //Soft delete friend to double guard showing up.
                //Relationship now has removed timestamp, and is soft deleted.
                await _repos.GetRepository<Friend>()
                    .SoftDeleteAsync(friend, saveNow: false);

            foreach (var pendingRequest in pendingRequests)
                await _repos.GetRepository<FriendRequest>()
                    .SoftDeleteAsync(pendingRequest, saveNow: false);

            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to process block {Report} for user {UserId}", 
                reportedUser != null ? "and report" : "", userId);
            return false;
        }
    }
    
    public async Task<bool> TryUnblockUser(string userId)
    {
        var result = await CheckUserId(userId);
        if (!result) return false;
        
        var blockedUser = await GetBlockedUser(userId);
        if (blockedUser == null) return true;
        
        await _repos.BeginTransactionAsync();
        try
        {
            await _repos.GetRepository<BlockedUser>()
                .SoftDeleteAsync(blockedUser, saveNow: false);
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to unblock user {UserId}", userId);
            return false;
        }
    }

}