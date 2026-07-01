using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using JC.Web.UI.Helpers;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.Boards;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.DataModels.Boards;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Models.ViewModels.BoardSkins;
using UltimateMonopoly.Services.Cache;

namespace UltimateMonopoly.Services.BoardSkins;

public class BoardSkinService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly ILogger<BoardSkinService> _logger;
    private readonly BoardCacheService _boardCacheService;
    private readonly BoardSkinShareService _boardSkinShareService;

    public BoardSkinService(IRepositoryManager repos,
        IUserInfo userInfo,
        ILogger<BoardSkinService> logger,
        BoardCacheService boardCacheService,
        BoardSkinShareService boardSkinShareService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _logger = logger;
        _boardCacheService = boardCacheService;
        _boardSkinShareService = boardSkinShareService;
    }


    public async Task<Board?> GetBoard(string? id)
    {
        Board fullBoard;
        if (string.IsNullOrWhiteSpace(id))
        {
            fullBoard = await _boardCacheService.GetDefaultBoard();
        }
        else
        {
            var allBoards = await _boardCacheService.GetAllBoards();
            var b = allBoards.FirstOrDefault(b => string.Equals(b.BoardId, id, StringComparison.OrdinalIgnoreCase));
            if (b == null) return null;

            fullBoard = b;
        }

        var spaces = fullBoard.Spaces
            .Where(s => s.SpaceType != BoardSpaceType.Chance && s.SpaceType != BoardSpaceType.ComChest)
            .ToList();
        return new Board(fullBoard.Name, spaces);
    }

    public async Task<List<SelectListItem>> GetBoardDropdown()
    {
        var boards = await GetAllBoardSkins(false);
        var shared = await _boardSkinShareService.GetSharedBoardSkins(false);
        
        boards.AddRange(shared);
        return DropdownHelper.FromCollection(boards, b => b.Name, b => b.Id);
    }
    

    private IQueryable<BoardSkin> QueryBoardSkins(bool asNoTracking, bool includeSpaces, DeletedQueryType deletedQueryType)
    {
        var query = _repos.GetRepository<BoardSkin>()
            .AsQueryable().FilterDeleted(deletedQueryType);

        if (asNoTracking)
            query = query.AsNoTracking();
        
        if(includeSpaces)
            query = query.Include(b => b.Spaces);
        
        return query.Where(b => b.UserId == _userInfo.UserId);
    }

    public async Task<List<BoardSkinViewModel>> GetAllBoardSkins(bool includeSpaces = true)
        => await QueryBoardSkins(true, includeSpaces, DeletedQueryType.OnlyActive)
            .Select(b => new BoardSkinViewModel(b))
            .ToListAsync();

    public async Task<BoardSkinViewModel?> GetBoardSkin(string id, bool includeSpaces = true)
    {
        var boardSkin = await QueryBoardSkins(false, includeSpaces, DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(b => b.Id == id);
        return boardSkin == null ? null : new BoardSkinViewModel(boardSkin);
    }

    public async Task<bool> ValidBoardSkin(string id)
        => await _repos.GetRepository<BoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => b.Id == id && (b.UserId == _userInfo.UserId 
                                          || b.SharedWith.Any(s => !s.IsDeleted && s.UserId == _userInfo.UserId)));
    
    private async Task ValidateBoardSkin(BoardSkin boardSkin, ModelStateWrapper modelState)
    {
        if(boardSkin.UserId != _userInfo.UserId)
            throw new InvalidOperationException("User is not the owner of this board skin");
        
        if(boardSkin.Name.Length > 128)
            modelState.AddModelError(nameof(boardSkin.Name), "Name must be less than 128 characters");
        
        if(!string.IsNullOrEmpty(boardSkin.Description) && boardSkin.Description.Length > 10240)
            modelState.AddModelError(nameof(boardSkin.Description), "Description must be less than 10,240 characters");
        
        if(string.IsNullOrWhiteSpace(boardSkin.Name))
            modelState.AddModelError(nameof(boardSkin.Name), "Name is required");
        
        var existingName = await _repos.GetRepository<BoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => b.Name.ToLower() == boardSkin.Name.ToLower() 
                           && b.UserId == _userInfo.UserId 
                           && b.Id != boardSkin.Id);
        if(existingName)
            modelState.AddModelError(nameof(boardSkin.Name), "A board already exists with this name");
    }

    private async Task<bool> TryCreateBoardSkin(BoardSkin boardSkin, ModelStateWrapper modelState)
    {
        boardSkin.UserId = _userInfo.UserId;
        await ValidateBoardSkin(boardSkin, modelState);
        if(!modelState.IsValid) return false;

        await _repos.GetRepository<BoardSkin>()
            .AddAsync(boardSkin);
        
        _boardCacheService.Invalidate();
        return true;
    }

    private async Task<bool> TryUpdateBoardSkin(BoardSkin boardSkin, ModelStateWrapper modelState)
    {
        await ValidateBoardSkin(boardSkin, modelState);
        if(!modelState.IsValid) return false;

        await _repos.GetRepository<BoardSkin>()
            .UpdateAsync(boardSkin);

        //Editing an already-shared board must clear every recipient's cache too (their cached
        //list holds this board's name/spaces), not just the owner's.
        await InvalidateSkinCachesAsync(boardSkin.Id);
        return true;
    }

    public async Task<SaveSkinResult> TrySaveSkin(string? skinId, string? name, string? description,
        ModelStateWrapper modelState)
    {
        if (string.IsNullOrWhiteSpace(skinId))
        {
            if(_userInfo.IsInRole(AppRoles.Restricted))
                return new SaveSkinResult(false, null);
            
            var skin = new BoardSkin { Name = name ?? string.Empty, Description = description };
            var created = await TryCreateBoardSkin(skin, modelState);
            return new SaveSkinResult(created, created ? skin.Id : null);
        }

        var existing = await QueryBoardSkins(asNoTracking: false, includeSpaces: false, DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(b => b.Id == skinId);
        if (existing == null)
        {
            modelState.AddModelError(string.Empty, "Board skin not found.");
            return new SaveSkinResult(false, null);
        }

        existing.Name = name ?? string.Empty;
        existing.Description = description;
        var updated = await TryUpdateBoardSkin(existing, modelState);
        return new SaveSkinResult(updated, updated ? existing.Id : null);
    }

    public async Task<bool> TryDeleteBoardSkin(string id)
    {
        var boardSkin = await QueryBoardSkins(false, true, DeletedQueryType.All)
            .FirstOrDefaultAsync(b => b.Id == id);
        if (boardSkin == null) return false;
        
        if(boardSkin.IsDeleted) return true;

        var spaces = boardSkin.Spaces;
        if (spaces == null!)
            spaces = await _repos.GetRepository<BoardSkinSpace>()
                .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
                .Where(bs => bs.BoardId == boardSkin.Id)
                .ToListAsync();
        else
            spaces = spaces.Where(s => !s.IsDeleted).ToList();

        var shareLinks = await _repos.GetRepository<SharedBoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(sbs => sbs.BoardSkinId == boardSkin.Id)
            .ToListAsync();
        
        await _repos.BeginTransactionAsync();
        try
        {
            if (spaces.Count > 0)
                await _repos.GetRepository<BoardSkinSpace>()
                    .SoftDeleteAsync(spaces, saveNow: false);

            if (shareLinks.Count > 0)
                await _repos.GetRepository<SharedBoardSkin>()
                    .SoftDeleteAsync(shareLinks, saveNow: false);
            
            await _repos.GetRepository<BoardSkin>()
                .SoftDeleteAsync(boardSkin, saveNow: false);
            
            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();

            //Clear the owner's cache and every recipient the board was shared with. The share
            //links are now soft-deleted, so an OnlyActive query would return nothing — use the
            //recipients captured before deletion.
            InvalidateSkinCaches(shareLinks.Select(sbs => sbs.UserId));
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to delete board skin {BoardSkinId}", id);
            return false;
        }
    }

    /// <summary>
    /// Invalidates the current user's (owner's) board cache and the caches of every user the
    /// board is actively shared with, so an owner-side edit propagates to shared recipients.
    /// </summary>
    private async Task InvalidateSkinCachesAsync(string boardSkinId)
    {
        var recipientUserIds = await _repos.GetRepository<SharedBoardSkin>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(sbs => sbs.BoardSkinId == boardSkinId)
            .Select(sbs => sbs.UserId)
            .Distinct()
            .ToListAsync();

        InvalidateSkinCaches(recipientUserIds);
    }

    /// <summary>
    /// Invalidates the current user's (owner's) board cache plus each supplied recipient cache.
    /// Recipients bypass the admin guard — the owner is not an admin, but must be able to clear
    /// the caches of users their board is shared with.
    /// </summary>
    private void InvalidateSkinCaches(IEnumerable<string> recipientUserIds)
    {
        _boardCacheService.Invalidate();
        foreach (var userId in recipientUserIds)
            _boardCacheService.Invalidate(userId, bypassAdminCheck: true);
    }
    
    
    
    #region Board Skin Spaces

    private async Task ValidateBoardSkinSpace(BoardSkinSpace boardSkinSpace, ModelStateWrapper modelState)
    {
        if(boardSkinSpace.Name.Length > 128)
            modelState.AddModelError(nameof(boardSkinSpace.Name), "Name must be less than 128 characters");
        
        if(string.IsNullOrWhiteSpace(boardSkinSpace.Name))
            modelState.AddModelError(nameof(boardSkinSpace.Name), "Name is required");
        
        var existingName = await _repos.GetRepository<BoardSkinSpace>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => b.Name.ToLower() == boardSkinSpace.Name.ToLower() 
                           && b.BoardId == boardSkinSpace.BoardId
                           && b.Id != boardSkinSpace.Id);
        if(existingName)
            modelState.AddModelError(nameof(boardSkinSpace.Name), "A board space on this board already has this name");
    }

    private async Task<bool> TryCreateBoardSkinSpace(BoardSkinSpace boardSkinSpace, ModelStateWrapper modelState)
    {
        await ValidateBoardSkinSpace(boardSkinSpace, modelState);
        if(!modelState.IsValid) return false;
        
        await _repos.GetRepository<BoardSkinSpace>()
            .AddAsync(boardSkinSpace);

        await InvalidateSkinCachesAsync(boardSkinSpace.BoardId);
        return true;
    }

    private async Task<bool> TryUpdateBoardSkinSpace(BoardSkinSpace boardSkinSpace, ModelStateWrapper modelState)
    {
        await ValidateBoardSkinSpace(boardSkinSpace, modelState);
        if(!modelState.IsValid) return false;
        
        await _repos.GetRepository<BoardSkinSpace>()
            .UpdateAsync(boardSkinSpace);

        await InvalidateSkinCachesAsync(boardSkinSpace.BoardId);
        return true;
    }

    private async Task<bool> TryDeleteBoardSkinSpace(string id)
    {
        var space = await _repos.GetRepository<BoardSkinSpace>()
            .AsQueryable()
            .FirstOrDefaultAsync(bs => bs.Id == id);
        if(space == null) return false;

        if(space.IsDeleted) return true;

        await _repos.GetRepository<BoardSkinSpace>()
            .DeleteAsync(space);

        await InvalidateSkinCachesAsync(space.BoardId);
        return true;
    }

    public async Task<bool> TrySaveSpace(string skinId, string? customSpaceId, ushort index, string? name,
        ModelStateWrapper modelState)
    {
        // Ownership check — QueryBoardSkins already filters to current user.
        var ownsSkin = await QueryBoardSkins(asNoTracking: true, includeSpaces: false, DeletedQueryType.OnlyActive)
            .AnyAsync(b => b.Id == skinId);
        if (!ownsSkin)
        {
            modelState.AddModelError(string.Empty, "Board skin not found.");
            return false;
        }

        // Resolve the space type from the default board so the user doesn't have to send it.
        var board = await GetBoard(null);
        var defaultSpace = board?.Spaces.FirstOrDefault(s => s.Index == index);
        if (defaultSpace == null)
        {
            modelState.AddModelError(nameof(BoardSkinSpace.Name), "Space cannot be customised.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(customSpaceId))
        {
            var newSpace = new BoardSkinSpace
            {
                BoardId = skinId,
                Name = name ?? string.Empty
            };
            if (!newSpace.SetSpaceProperties(index, defaultSpace.SpaceType))
            {
                modelState.AddModelError(nameof(BoardSkinSpace.Name), "Space cannot be customised.");
                return false;
            }
            return await TryCreateBoardSkinSpace(newSpace, modelState);
        }

        var existing = await _repos.GetRepository<BoardSkinSpace>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(s => s.Id == customSpaceId && s.BoardId == skinId);
        if (existing == null)
        {
            modelState.AddModelError(string.Empty, "Custom space not found.");
            return false;
        }

        existing.Name = name ?? string.Empty;
        return await TryUpdateBoardSkinSpace(existing, modelState);
    }

    public async Task<bool> TryDeleteSpace(string skinId, string customSpaceId)
    {
        var ownsSkin = await QueryBoardSkins(asNoTracking: true, includeSpaces: false, DeletedQueryType.OnlyActive)
            .AnyAsync(b => b.Id == skinId);
        if (!ownsSkin) return false;

        var belongs = await _repos.GetRepository<BoardSkinSpace>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(s => s.Id == customSpaceId && s.BoardId == skinId);
        if (!belongs) return false;

        return await TryDeleteBoardSkinSpace(customSpaceId);
    }

    #endregion
}