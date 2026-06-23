using System.Text.Json;
using JC.Core.Models;
using JC.Identity.Authentication;
using JC.Web.Security.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Pages;
using IndexModel = UltimateMonopoly.Pages.Leaderboard.IndexModel;

namespace UltimateMonopoly.Services;

public class ProfileService
{
    public const string CookieName = "user-profile";
    public const string ProtectorPurpose = "UserProfileProtector";
    public const string ImgFileType = ".png";

    //Note: your avatar image is a game piece
    private readonly string[] _avatarImageNames = ["car", "dog", "cat", "van", "horse", "wheel_barrow", "boot", "battleship", "plane"];

    private readonly FilePathProvider _filePathProvider;
    private readonly IUserInfo _userInfo;
    private readonly ICookieService _cookies;
    private readonly UrlLinkService _urlLinkService;
    private readonly UserService _userService;
    private readonly AppDbContext _context;
    private readonly UserManager<AppUser> _userManager;
    private readonly SignInManager<AppUser> _signInManager;

    public ProfileService(
        FilePathProvider filePathProvider,
        IUserInfo userInfo,
        [FromKeyedServices(ICookieService.EncryptedCookieDIKey)] ICookieService cookies,
        UrlLinkService urlLinkService,
        UserService userService,
        AppDbContext context,
        UserManager<AppUser> userManager,
        SignInManager<AppUser> signInManager)
    {
        _filePathProvider = filePathProvider;
        _userInfo = userInfo;
        _cookies = cookies;
        _urlLinkService = urlLinkService;
        _userService = userService;
        _context = context;
        _userManager = userManager;
        _signInManager = signInManager;
    }

    public List<string> GetAvatarImagePaths()
        => (from imgName in _avatarImageNames
            let path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.ProfileImg)
            select Path.Combine(path, imgName + ImgFileType)
            into path where File.Exists(path) select path).ToList();

    public IReadOnlyList<string> GetAvailableAvatarImageNames()
    {
        var dir = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.ProfileImg);
        return _avatarImageNames
            .Where(name => File.Exists(Path.Combine(dir, name + ImgFileType)))
            .ToList();
    }

    public string? GetAvatarImagePath(string name)
    {
        if (!_avatarImageNames.Contains(name)) return null;
        var path = Path.Combine(
            _filePathProvider.GetFilePath(FilePathProvider.FileCategory.ProfileImg),
            name + ImgFileType);
        return File.Exists(path) ? path : null;
    }


    #region Hidden Users

    public async Task<List<string>> GetHiddenUserIds(List<UserProfileViewModel> profiles)
    {
        var userIds = profiles.Select(p => p.UserId).ToHashSet();
        var hiddenRoleId = await _context.Roles.FirstOrDefaultAsync(r => r.Name == AppRoles.HiddenUser);
        if(hiddenRoleId == null)
            throw new Exception("Hidden user role not found"); //should seed in program.cs, so defensive code
        
        return await _context.UserRoles.Where(ur => ur.RoleId == hiddenRoleId.Id 
                                                               && userIds.Contains(ur.UserId) 
                                                               && _userInfo.UserId != ur.UserId)
            .Select(ur => ur.UserId)
            .ToListAsync();
    }

    public async Task<bool> TryHideUser(string? userId = null)
    {
        if(!string.IsNullOrEmpty(userId) && !_userInfo.IsInRole(SystemRoles.SystemAdmin) && !_userInfo.IsInRole(SystemRoles.Admin))
            return false;
        
        userId ??= _userInfo.UserId;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.IsEnabled && u.Id == userId);
        if(user == null) return false;
        
        var alreadyHidden = await _userManager.IsInRoleAsync(user, AppRoles.HiddenUser);
        if (alreadyHidden) return true;
        
        var result = await _userManager.AddToRoleAsync(user, AppRoles.HiddenUser);
        if(!result.Succeeded) return false;
        
        if(user.Id == _userInfo.UserId)
            await _signInManager.RefreshSignInAsync(user);
        return true;
    }
    
    public async Task<bool> TryUnhideUser(string? userId = null)
    {
        if(!string.IsNullOrEmpty(userId) && !_userInfo.IsInRole(SystemRoles.SystemAdmin) && !_userInfo.IsInRole(SystemRoles.Admin))
            return false;
        
        userId ??= _userInfo.UserId;
        var user = await _context.Users.FirstOrDefaultAsync(u => u.IsEnabled && u.Id == userId);
        if(user == null) return false;
        
        var alreadyHidden = await _userManager.IsInRoleAsync(user, AppRoles.HiddenUser);
        if (!alreadyHidden) return true;
        
        var result = await _userManager.RemoveFromRoleAsync(user, AppRoles.HiddenUser);
        if(!result.Succeeded) return false;
        
        if(user.Id == _userInfo.UserId)
            await _signInManager.RefreshSignInAsync(user);
        return true;       
    }

    /// <summary>
    /// Whether the user (defaults to the current user) is hidden from the public leaderboard — i.e.
    /// in the <see cref="AppRoles.HiddenUser"/> role. DB-queried (not claims) so a freshly-applied
    /// toggle is reflected immediately, with no cookie refresh.
    /// </summary>
    public async Task<bool> IsHidden(string? userId = null)
    {
        userId ??= _userInfo.UserId;
        if (string.IsNullOrEmpty(userId)) return false;

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
        return user != null && await _userManager.IsInRoleAsync(user, AppRoles.HiddenUser);
    }

    #endregion
    

    public Task<UserProfileViewModel?> GetCurrentUserProfileViewModelAsync()
        => GetUserProfileViewModelAsync(_userInfo.UserId
            ?? throw new InvalidOperationException("No authenticated user"));

    public async Task<UserProfileViewModel?> GetUserProfileViewModelAsync(string userId)
    {
        bool? enabled = userId == _userInfo.UserId ? null : true;
        var user = await _userService.GetUserById(userId, enabled);
        if (user == null) return null;

        var imgUrl = _urlLinkService.GetImgUrl(user.AvatarImageName);
        return new UserProfileViewModel(user, imgUrl);
    }

    public async Task<List<UserProfileViewModel>> GetUserProfilesForLeaderboard()
    {
        //Get a list of enabled users where they have played at least one game
        var users = await _context.Users
            .Where(u => u.IsEnabled 
                        && (u.NumberOfWins + u.NumberOfDraws + u.NumberOfLosses >= IndexModel.MinimumGames))
            .ToListAsync();

        return (from u in users
            let imgUrl = _urlLinkService.GetImgUrl(u.AvatarImageName)
            select new UserProfileViewModel(u, imgUrl))
            .OrderBy(u => string.IsNullOrEmpty(u.DisplayName) ? u.Username : u.DisplayName)
            .ToList();
    }

    // Builds the view model from the profile cookie when it belongs to the
    // requested user; otherwise falls back to the database.
    public async Task<UserProfileViewModel?> GetProfileViewModelAsync(string userId)
    {
        var cached = ReadCookie();
        if (cached is not null && cached.UserId == userId && cached.Username is not null)
            return new UserProfileViewModel(cached.UserId, cached.Username, cached.DisplayName,
                cached.AvatarColour, _urlLinkService.GetImgUrl(cached.AvatarImageName));

        return await GetUserProfileViewModelAsync(userId);
    }

    public async Task<UserProfile> GetAsync()
    {
        var userId = _userInfo.UserId ?? throw new InvalidOperationException("No authenticated user");

        var cached = ReadCookie();
        if (cached?.UserId == userId)
            return new UserProfile(cached.AvatarColour, cached.AvatarImageName);

        if (cached is not null)
            _cookies.TryDeleteCookie(CookieName);

        var user = await _userService.GetUserById(userId, null);
        if (user == null)
            return new UserProfile(null, null);
        
        var profile = new UserProfile(user.AvatarColour, user.AvatarImageName);
        WriteCookie(profile);
        return profile;
    }

    public async Task<bool> TryUpdateAsync(UserProfile updated)
    {
        var userId = _userInfo.UserId ?? throw new InvalidOperationException("No authenticated user");

        if (updated.AvatarImageName is not null && !_avatarImageNames.Contains(updated.AvatarImageName))
            return false;

        var user = await _userService.GetUserById(userId, null);
        if (user == null) return false;
        
        var updatedColour = user.SetAvatarColour(updated.AvatarColour);
        user.AvatarImageName = updated.AvatarImageName;
        await _context.SaveChangesAsync();

        var newProfile = updated with { AvatarColour = updatedColour };
        WriteCookie(newProfile);
        return true;
    }

    private CookiePayload? ReadCookie()
    {
        var raw = _cookies.GetCookie(CookieName);
        if (raw is null) return null;
        try { return JsonSerializer.Deserialize<CookiePayload>(raw); }
        catch { return null; }
    }

    private void WriteCookie(UserProfile profile)
        => _cookies.TryCreateCookie(CookieName, JsonSerializer.Serialize(
            new CookiePayload(_userInfo.UserId, _userInfo.Username, _userInfo.DisplayName,
                profile.AvatarColour, profile.AvatarImageName)));

    private record CookiePayload(string UserId, string? Username, string? DisplayName,
        string? AvatarColour, string? AvatarImageName);
}