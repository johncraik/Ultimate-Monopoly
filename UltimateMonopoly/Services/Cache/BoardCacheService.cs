using JC.Core.Models;
using JC.Identity.Authentication;
using Microsoft.Extensions.Caching.Memory;
using MP.GameEngine.Models.Boards;
using UltimateMonopoly.Services.Imports;

namespace UltimateMonopoly.Services.Cache;

public class BoardCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly IUserInfo _userInfo;
    private readonly BoardImportService _boardImportService;

    private const string CacheKey = "Boards";
    private const string DefaultBoardKey = "<DEFAULT>";
    private static readonly TimeSpan CustomBoardExpiration = TimeSpan.FromHours(6);

    public BoardCacheService(IMemoryCache memoryCache,
        IUserInfo userInfo,
        BoardImportService boardImportService)
    {
        _memoryCache = memoryCache;
        _userInfo = userInfo;
        _boardImportService = boardImportService;
    }

    private string GetKey(bool isDefault, string? userId = null)
        => $"{CacheKey}__{(isDefault ? DefaultBoardKey : userId ?? _userInfo.UserId)}";

    public async Task<Board> GetDefaultBoard()
        => await _memoryCache.GetOrCreateAsync(GetKey(true), entry =>
            {
                entry.Priority = CacheItemPriority.NeverRemove;
                return _boardImportService.ImportDefaultBoard();
            }) ?? throw new InvalidOperationException("Failed to get default board");

    public async Task<List<Board>> GetAllBoards(bool includeDefault = true, string? userId = null)
    {
        var defaultBoard = await GetDefaultBoard();

        var customBoards = await _memoryCache.GetOrCreateAsync(GetKey(false, userId), entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = CustomBoardExpiration;
            return _boardImportService.GetBoardSkins(defaultBoard, userId);
        }) ?? throw new InvalidOperationException("Failed to get custom boards");

        return includeDefault 
            ? [defaultBoard, ..customBoards] 
            : customBoards;
    }

    public void Invalidate(string? userId = null, bool bypassAdminCheck = false)
    {
        if (!bypassAdminCheck && !string.IsNullOrEmpty(userId) && !_userInfo.IsInRole(SystemRoles.SystemAdmin))
            return;
        
        _memoryCache.Remove(GetKey(false, userId));
    }
}