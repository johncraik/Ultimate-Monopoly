using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Models.Pagination;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Social;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services;

namespace UltimateMonopoly.Areas.Social.Services;

public class BlockAndReportService
{
    private readonly IRepositoryManager _repos;
    private readonly AppDbContext _context;
    private readonly IUserInfo _userInfo;
    private readonly UrlLinkService _urlLinkService;
    private readonly ILogger<BlockAndReportService> _logger;

    public BlockAndReportService(IRepositoryManager repos,
        AppDbContext context,
        IUserInfo userInfo,
        UrlLinkService urlLinkService,
        ILogger<BlockAndReportService> logger)
    {
        _repos = repos;
        _context = context;
        _userInfo = userInfo;
        _urlLinkService = urlLinkService;
        _logger = logger;
    }

    public async Task<PagedList<UserProfileViewModel>> GetBlockedUsers(ushort pageNumber, ushort pageSize)
    {
        var blockedUsers = await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(b => b.CreatedById == _userInfo.UserId)
            .ToListAsync();
        
        var userIds = blockedUsers.Select(b => b.BlockedUserId).Distinct().ToList();
        var users = await _context.Users.Where(u => u.IsEnabled && userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id);

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

    
    private async Task<BlockedUser?> GetBlockedUser(string userId) 
        => await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(b => b.CreatedById == _userInfo.UserId && b.BlockedUserId == userId);
    
    private async Task<bool> CheckUserId(string userId)
        => userId != _userInfo.UserId && await _context.Users.AnyAsync(u => u.Id == userId && u.IsEnabled);
    
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
        if (blockedUser != null)
        {
            friend = await _repos.GetRepository<Friend>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
                .FirstOrDefaultAsync(f => (f.CreatedById == _userInfo.UserId && f.FriendUserId == userId) 
                                          || (f.FriendUserId == _userInfo.UserId && f.CreatedById == userId));
            friend?.Remove();
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
                //TODO: Decide if Unblocking would create new friend record.
                await _repos.GetRepository<Friend>()
                    .SoftDeleteAsync(friend, saveNow: false);
            
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

        //TODO: Decide if friend relationship should be readded when unlock
        
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