using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;

namespace UltimateMonopoly.Services;

public class UserService
{
    private readonly AppDbContext _context;

    public UserService(AppDbContext context)
    {
        _context = context;
    }


    public async Task<bool> ValidUser(string userId)
        => await _context.Users.AnyAsync(u => u.IsEnabled && userId == u.Id);
    
    public async Task<int> CountValidUserIds(IEnumerable<string> userIds)
        => await _context.Users.CountAsync(u => u.IsEnabled && userIds.Contains(u.Id));

    private IQueryable<AppUser> QueryUsers(bool? enabledFilter)
    {
        var query = _context.Users.AsQueryable();
        
        if (enabledFilter.HasValue)
            query = query.Where(u => u.IsEnabled == enabledFilter.Value);
        
        return query;
    }
    
    public async Task<Dictionary<string, AppUser>> GetUserDictionary(bool? enabledFilter = true)
        => await QueryUsers(enabledFilter)
            .ToDictionaryAsync(u => u.Id, u => u);
    
    public async Task<Dictionary<string, AppUser>> GetUserDictionary(IEnumerable<string> userIds, bool? enabledFilter = true)
        => await QueryUsers(enabledFilter)
            .Where(u => userIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u);
    
    public async Task<AppUser?> GetUserById(string userId, bool? enabledFilter = true)
        => await QueryUsers(enabledFilter)
            .FirstOrDefaultAsync(u => u.Id == userId);
    
    public async Task<AppUser?> GetUserByUsername(string username, bool? enabledFilter = true)
        => await QueryUsers(enabledFilter)
            .FirstOrDefaultAsync(u => !string.IsNullOrEmpty(u.UserName) 
                                      && u.UserName!.ToLower() == username.ToLower());
}