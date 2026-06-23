using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using UltimateMonopoly.Models.ViewModels.Leaderboard;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services.Cache;
using UltimateMonopoly.Services.Friends;

namespace UltimateMonopoly.Services.Statistics;

public class LeaderboardService
{
    private readonly IUserInfo _userInfo;
    private readonly ProfileService _profileService;
    private readonly BlockAndReportService _blockAndReportService;
    private readonly FriendService _friendService;

    public LeaderboardService(IUserInfo userInfo,
        ProfileService profileService,
        BlockAndReportService blockAndReportService,
        FriendService friendService)
    {
        _userInfo = userInfo;
        _profileService = profileService;
        _blockAndReportService = blockAndReportService;
        _friendService = friendService;
    }
    
    // Public-facing leaderboard: ranks all qualifying players by overall score (see PlayerScore),
    // showing colour, avatar, and name plus W/D/L, games played, and win rate. Detailed side-by-side
    // stats (the Compare page) are friends-only.

    public async Task<List<LeaderboardRecord>> GetLeaderboard()
    {
        var userProfiles = await _profileService.GetUserProfilesForLeaderboard();
        var blockedProfiles = await _blockAndReportService.CheckAndReportExistingBlocks(_userInfo.UserId, 
            userProfiles.Select(u => u.UserId));
        var friendProfiles = await _friendService.AreFriends(userProfiles.Select(u => u.UserId));
        var hiddenUserIds = await _profileService.GetHiddenUserIds(userProfiles);
        
        var leaderboardRecords = new List<LeaderboardRecord>();
        foreach (var up in userProfiles)
        {
            var blockCheck = blockedProfiles.FirstOrDefault(b => b.userId == up.UserId);
            var friendCheck = friendProfiles.FirstOrDefault(f => f.userId == up.UserId);
            var hidden = hiddenUserIds.Contains(up.UserId);
            
            if(blockCheck == default || !blockCheck.Blocked)
            {
                var profile = up;
                if(!friendCheck.Firends && hidden)
                    profile = new UserProfileViewModel(up.NumberOfWins, up.NumberOfLosses, up.NumberOfDraws);
                
                var lr = new LeaderboardRecord(profile, friendCheck.Firends);
                leaderboardRecords.Add(lr);
                continue;
            }

            var unknownProfile = new UserProfileViewModel(up.NumberOfWins, up.NumberOfLosses, up.NumberOfDraws);
            leaderboardRecords.Add(new LeaderboardRecord(unknownProfile, false));
        }
        
        //Ranked by complex ordering
        return leaderboardRecords
            .OrderByDescending(l => PlayerScore(l.UserProfile))
            .ThenByDescending(l => l.UserProfile.NumberOfWins)
            .ThenByDescending(l => PlayerWinRate(l.UserProfile))
            .ThenBy(l => l.UserProfile.NumberOfLosses)
            .ToList();
    }

    public static decimal PlayerScore(UserProfileViewModel profile)
    {
        //Extract win/loss/draw
        var wins = profile.NumberOfWins;
        var losses = profile.NumberOfLosses;
        var draws = profile.NumberOfDraws;
        
        //Total games played
        long gamesPlayed = wins + losses + draws;
        
        var winRate = PlayerWinRate(profile);

        //Penalty for low games played (less than 10 games)
        var lowGamesPenalty = Math.Max(0, 10 - gamesPlayed) * 20m;
                
        //Calculate rating
        var score = 1000m
                     + (wins * 30m)
                     + (draws * 10m)
                     - (losses * 15m)
                     + (winRate * 200m)
                     - lowGamesPenalty;
                
        //Order by rating
        return score;
    }

    public static decimal PlayerWinRate(UserProfileViewModel profile)
    {
        //Extract win/loss/draw
        var wins = profile.NumberOfWins;
        var losses = profile.NumberOfLosses;
        var draws = profile.NumberOfDraws;
                
        //Total games played
        long gamesPlayed = wins + losses + draws;
                
        //Win rate
        return gamesPlayed == 0
            ? 0m
            : (decimal)wins / gamesPlayed;
    }
}