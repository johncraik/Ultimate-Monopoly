using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.DataModels.Social;
using UltimateMonopoly.Models.ViewModels.BoardSkins;
using UltimateMonopoly.Services.GameConfig;

namespace UltimateMonopoly.Services.BoardSkins;

public class BoardSkinShareService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly ILogger<BoardSkinShareService> _logger;
    private readonly BoardCacheService _boardCacheService;
    private readonly UserService _userService;

    public BoardSkinShareService(IRepositoryManager repos,
        IUserInfo userInfo,
        ILogger<BoardSkinShareService> logger,
        BoardCacheService boardCacheService,
        UserService userService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _logger = logger;
        _boardCacheService = boardCacheService;
        _userService = userService;
    }


    private IQueryable<BoardSkin> GetSharedBoardSkinsQuery(bool asNoTracking, bool includeSpaces,
        DeletedQueryType deletedQueryType)
    {
        var query = _repos.GetRepository<BoardSkin>()
            .AsQueryable().FilterDeleted(deletedQueryType)
            .Where(b => b.SharedWith.Any(s => !s.IsDeleted && s.UserId == _userInfo.UserId));
        
        if(asNoTracking)
            query = query.AsNoTracking();
        
        if(includeSpaces)
            query = query.Include(b => b.Spaces);
        
        return query;
    }
    
    public async Task<List<BoardSkinViewModel>> GetSharedBoardSkins(bool includeSpaces = true)
        => await GetSharedBoardSkinsQuery(true, includeSpaces, DeletedQueryType.OnlyActive)
            .Select(b => new BoardSkinViewModel(b))
            .ToListAsync();

    public async Task<BoardSkinViewModel?> GetSharedBoardSkin(string id, bool includeSpaces = true)
    {
        var boardSkin = await GetSharedBoardSkinsQuery(false, includeSpaces, DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(b => b.Id == id);
        return boardSkin == null ? null : new BoardSkinViewModel(boardSkin);
    }

    public async Task<List<string>> GetUserIdsForSharedBoardSkin(string skinId)
        => await _repos.GetRepository<SharedBoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(sbs => sbs.BoardSkinId == skinId && sbs.BoardSkin.UserId == _userInfo.UserId)
            .Select(sbs => sbs.UserId)
            .ToListAsync();
    

    private async Task<bool> ValidBoardSkin(string skinId, bool checkUserId = true)
        => await _repos.GetRepository<BoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => b.Id == skinId && (!checkUserId || b.UserId == _userInfo.UserId));

    private async Task<bool> ValidUserIds(List<string> userIds)
    {
        var distinct = userIds.Distinct().ToList();                                                                                                                                                                                            
        if (distinct.Count == 0) return true;                                                                                                                                                                                                  
                                                                                                                                                                                                                                             
        var validCount = await _userService.CountValidUserIds(distinct);                                                                                                                                                                                                                                                                                                                                             
        if(validCount != distinct.Count) return false;

        var friendCount = await _repos.GetRepository<Friend>()                                                                                                                                                                                     
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)                                                                                                                                                                              
            .CountAsync(f => f.DateRemovedUtc == null                                                                                                                                                                                              
                             && ((f.CreatedById == _userInfo.UserId && distinct.Contains(f.FriendUserId))                                                                                                                                                       
                                 || (f.FriendUserId == _userInfo.UserId && distinct.Contains(f.CreatedById!))));                                                                                                                                                
        return friendCount == distinct.Count;
    }
    
    public async Task<bool> TryShareBoardSkin(string skinId, List<string> userIds)
    {
        var valid = await ValidBoardSkin(skinId);
        if (!valid) return false;
        
        valid = await ValidUserIds(userIds);
        if (!valid) return false;

        var existingLinks = await _repos.GetRepository<SharedBoardSkin>()
            .AsQueryable().Where(sbs => sbs.BoardSkinId == skinId)
            .ToListAsync();

        var toRestore = existingLinks.Where(sbs => sbs.IsDeleted && userIds.Contains(sbs.UserId)).ToList();
        var toDelete = existingLinks.Where(sbs => !sbs.IsDeleted && !userIds.Contains(sbs.UserId)).ToList();

        var existingUserIds = existingLinks.Select(el => el.UserId).ToList();
        var toAdd = userIds.Where(u => !existingUserIds.Contains(u))
            .Select(u => new SharedBoardSkin(skinId, u))
            .ToList();

        if(toAdd.Count == 0 && toRestore.Count == 0 && toDelete.Count == 0)
            return true;
        
        await _repos.BeginTransactionAsync();
        try
        {
            if(toRestore.Count > 0)
                await _repos.GetRepository<SharedBoardSkin>()
                    .RestoreAsync(toRestore, saveNow: false);
            
            if(toDelete.Count > 0)
                await _repos.GetRepository<SharedBoardSkin>()
                    .SoftDeleteAsync(toDelete, saveNow: false);
            
            if(toAdd.Count > 0)
                await _repos.GetRepository<SharedBoardSkin>()
                    .AddAsync(toAdd, saveNow: false);
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();

            foreach (var userId in userIds)
            {
                _boardCacheService.Invalidate(userId);
            }
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to update board skin shares for skin {BoardSkinId}", skinId);
            return false;
        }
    }

    public async Task<bool> TryRemoveSharedBoardSkin(string skinId)
    {
        var valid = await ValidBoardSkin(skinId, false);
        if (!valid) return false;
        
        var shareLink = await _repos.GetRepository<SharedBoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(sbs => sbs.BoardSkinId == skinId && sbs.UserId == _userInfo.UserId);
        if (shareLink == null) return false;
        
        await _repos.GetRepository<SharedBoardSkin>()
            .SoftDeleteAsync(shareLink);
        
        _boardCacheService.Invalidate(_userInfo.UserId);
        return true;
    }
}