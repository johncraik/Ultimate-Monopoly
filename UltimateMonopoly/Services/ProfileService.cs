using System.Text.Json;
using JC.Core.Models;
using JC.Web.Security.Services;
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

    //TODO: to be extended with pieces that are in the game
    //Note: your avatar image is a game piece
    private readonly string[] _avatarImageNames = ["car", "dog", "cat", "van", "horse", "wheel_barrow", "boot", "battleship", "plane"];

    private readonly FilePathProvider _filePathProvider;
    private readonly IUserInfo _userInfo;
    private readonly ICookieService _cookies;
    private readonly UrlLinkService _urlLinkService;
    private readonly UserService _userService;
    private readonly AppDbContext _context;

    public ProfileService(
        FilePathProvider filePathProvider,
        IUserInfo userInfo,
        [FromKeyedServices(ICookieService.EncryptedCookieDIKey)] ICookieService cookies,
        UrlLinkService urlLinkService,
        UserService userService,
        AppDbContext context)
    {
        _filePathProvider = filePathProvider;
        _userInfo = userInfo;
        _cookies = cookies;
        _urlLinkService = urlLinkService;
        _userService = userService;
        _context = context;
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

    public Task<UserProfileViewModel?> GetCurrentUserProfileViewModelAsync()
        => GetUserProfileViewModelAsync(_userInfo.UserId
            ?? throw new InvalidOperationException("No authenticated user"));

    public async Task<UserProfileViewModel?> GetUserProfileViewModelAsync(string userId)
    {
        var user = await _userService.GetUserById(userId);
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