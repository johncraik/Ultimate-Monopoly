using JC.Communication.Notifications.Models;
using JC.Communication.Notifications.Services;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;
using UltimateMonopoly.Models;
using UltimateMonopoly.Models.DataModels.Social;
using UltimateMonopoly.Models.ViewModels;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Services.Friends;

public class FriendService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly PresenceService _presence;
    private readonly UrlLinkService _urlLinkService;
    private readonly NotificationSender _notifications;
    private readonly ILogger<FriendService> _logger;
    private readonly UserService _userService;

    public FriendService(IRepositoryManager repos,
        IUserInfo userInfo,
        PresenceService presence,
        UrlLinkService urlLinkService,
        NotificationSender notifications,
        ILogger<FriendService> logger,
        UserService userService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _presence = presence;
        _urlLinkService = urlLinkService;
        _notifications = notifications;
        _logger = logger;
        _userService = userService;
    }


    public async Task<bool> AreFriends(string userId1, string? userId2 = null)
    {
        userId2 ??= _userInfo.UserId;
        
        return await _repos.GetRepository<Friend>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(f => f.DateRemovedUtc == null 
                           && ((f.CreatedById == userId1 && f.FriendUserId == userId2) 
                               || (f.FriendUserId == userId1 && f.CreatedById == userId2)));
    }

    public async Task<List<(bool Firends, string userId)>> AreFriends(IEnumerable<string> userIds, string? userId = null)
    {
        userId ??= _userInfo.UserId;
        
        var list = userIds.ToList();
        var friends = await _repos.GetRepository<Friend>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(f => f.DateRemovedUtc == null
                        && ((f.CreatedById == userId && list.Contains(f.FriendUserId))
                            || (f.FriendUserId == userId && list.Contains(f.CreatedById!))))
            .Select(f => new
            {
                UserId = f.CreatedById == userId ? f.FriendUserId : f.CreatedById
            })
            .ToListAsync();

        return list.Select(u => (friends.Any(f => f.UserId == u), u)).ToList();
    }

    public async Task<List<FriendViewModel>> GetFriendsList()
    {
        var currentUserId = _userInfo.UserId;

        var friends = await _repos.GetRepository<Friend>()
            .AsQueryable()
            .Where(f => !f.IsDeleted && 
                        f.DateRemovedUtc == null
                        && (f.CreatedById == currentUserId
                            || f.FriendUserId == currentUserId))
            .ToListAsync();

        if (friends.Count == 0)
            return [];

        var friendUserIds = friends
            .Select(f => f.CreatedById == currentUserId ? f.FriendUserId : f.CreatedById!)
            .Distinct()
            .ToList();

        var blockedIds = await GetBlockedUserIds(friendUserIds);

        var users = await _userService.GetUserDictionary(friendUserIds);
        var lastActive = await _presence.GetLastActiveUtcAsync(friendUserIds);

        var result = new List<FriendViewModel>(friends.Count);
        foreach (var friend in friends)
        {
            var friendUserId = friend.CreatedById == currentUserId
                ? friend.FriendUserId
                : friend.CreatedById!;

            if (blockedIds.Contains(friendUserId))
                continue;

            if (!users.TryGetValue(friendUserId, out var user))
                continue;

            var lastSeen = lastActive.TryGetValue(friendUserId, out var ts) ? ts : null;
            var isOnline = _presence.IsOnline(friendUserId);

            var imgUrl = _urlLinkService.GetImgUrl(user.AvatarImageName);
            result.Add(new FriendViewModel(
                currentUserId,
                friend,
                user,
                imgUrl,
                lastSeen,
                isOnline));
        }

        return result.OrderBy(f => f.DisplayName).ToList();
    }


    #region Friend Requests


    public async Task<FriendRequestLists> GetFriendRequests()
    {
        var allRequests = await _repos.GetRepository<FriendRequest>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(r => r.IsAccepted == null && (r.CreatedById == _userInfo.UserId || r.ToUserId == _userInfo.UserId))
            .ToListAsync();

        var userIds = allRequests.Select(r => r.CreatedById == _userInfo.UserId ? r.ToUserId : r.CreatedById)
            .Distinct().ToList();
        var blockedIds = await GetBlockedUserIds(userIds!);
        var users = await _userService.GetUserDictionary(userIds!);
        
        var incomingRequests = new List<FriendRequestViewModel>();
        var outgoingRequests = new List<FriendRequestViewModel>();
        foreach (var request in allRequests)
        {
            var createdId = request.CreatedById ?? throw new InvalidOperationException("User ID not set");

            var outgoing = request.CreatedById == _userInfo.UserId;
            var otherUserId = outgoing ? request.ToUserId : createdId;
            if (blockedIds.Contains(otherUserId))
                continue;
            if(!users.TryGetValue(otherUserId, out var user))
                continue;
            
            var imgUrl = _urlLinkService.GetImgUrl(user.AvatarImageName);
            var requestViewModel = new FriendRequestViewModel(_userInfo.UserId, request, user, imgUrl);
            if(outgoing)
                outgoingRequests.Add(requestViewModel);
            else
                incomingRequests.Add(requestViewModel);
        }
        
        return new FriendRequestLists
        {
            IncomingRequests = incomingRequests,
            OutgoingRequests = outgoingRequests
        };
    }


    public async Task<bool> TryRemoveFriend(string friendId)
    {
        var userExists = await _userService.ValidUser(friendId);
        if (!userExists) return false;
        
        var friend = await _repos.GetRepository<Friend>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(f => (f.CreatedById == _userInfo.UserId && f.FriendUserId == friendId) 
                                      || (f.FriendUserId == _userInfo.UserId && f.CreatedById == friendId));
        if (friend == null) return true;
        
        friend.Remove();
        await _repos.GetRepository<Friend>()
            .SoftDeleteAsync(friend);
        return true;
    }
    
    
    public async Task<FriendRequestResult> TrySendFriendRequest(string friendUsername)
    {
        if(_userInfo.IsInRole(AppRoles.Restricted))
            return new FriendRequestResult(false, "Your account is restricted and cannot send friend requests.");
        
        //Get The user:
        var user = await _userService.GetUserByUsername(friendUsername);
        if (user == null) return new FriendRequestResult(false, "No user exists with this username.");
        
        //Check if self:
        if(string.Equals(user.Id, _userInfo.UserId, StringComparison.OrdinalIgnoreCase))
            return new FriendRequestResult(false, "Cannot send friend request to yourself.");
        
        //Check if pending:
        var pendingRequests = await _repos.GetRepository<FriendRequest>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(f => f.IsAccepted == null 
                        && ((f.CreatedById == user.Id && f.ToUserId == _userInfo.UserId) 
                            || (f.ToUserId == user.Id && f.CreatedById == _userInfo.UserId)))
            .ToListAsync();
        if (pendingRequests.Count > 0)
        {
            var anyOutgoing = pendingRequests.Any(r => r.CreatedById == _userInfo.UserId);
            var anyIncoming = pendingRequests.Any(r => r.ToUserId == _userInfo.UserId);
            if(anyIncoming)
                return new FriendRequestResult(false, "Incoming friend request already pending.");
            
            return anyOutgoing 
                ? new FriendRequestResult(false, "Outgoing friend request already pending.") 
                : new FriendRequestResult(false, "Friend request already pending.");
        }
        
        //Check if already friends:
        var friends = await _repos.GetRepository<Friend>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(f => f.DateRemovedUtc == null 
                           && ((f.CreatedById == _userInfo.UserId && f.FriendUserId == user.Id) 
                               || (f.FriendUserId == _userInfo.UserId && f.CreatedById == user.Id)));
        if (friends) return new FriendRequestResult(false, "Already friends.");
        
        //Check if blocked:
        var isBlocked = await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => (b.BlockedUserId == user.Id && b.CreatedById == _userInfo.UserId) 
                           || (b.CreatedById == user.Id && b.BlockedUserId == _userInfo.UserId));
        if (isBlocked) 
            //Message is ambiguous so that it is not revealed that someone blocked you
            return new FriendRequestResult(false, "No user exists with this username.");
        
        var request = new FriendRequest(user.Id);
        await _repos.GetRepository<FriendRequest>()
            .AddAsync(request);

        var senderName = string.IsNullOrWhiteSpace(_userInfo.DisplayName) ? _userInfo.Username : _userInfo.DisplayName;
        var notification = await _notifications.SendNotification(
            userId: user.Id,
            title: "New Friend Request",
            body: $"{senderName} sent you a friend request.",
            type: NotificationType.Message,
            link: "/social/friends?tab=requests");
        if (!notification.IsValid)
            _logger.LogWarning("Failed to send friend-request notification to {UserId}: {Error}",
                user.Id, notification.ErrorMessage);

        return new FriendRequestResult(true, null);
    }

    public async Task<bool> TryAcceptFriendRequest(string requestId)
    {
        var request = await _repos.GetRepository<FriendRequest>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.IsAccepted == null && r.ToUserId == _userInfo.UserId);
        if (request == null) return false;

        var originalSenderId = request.CreatedById ?? throw new InvalidOperationException("User ID not set");

        // Block guard: a block either way severs the relationship — don't let a request
        // that predates the block be accepted into a friendship.
        if (await IsBlockedBetween(originalSenderId))
            return false;

        request.Accept();
        var friend = new Friend();
        friend.Add(originalSenderId);

        var ok = await ProcessFriendRequest(request, friend);
        if (!ok) return false;

        var accepterName = string.IsNullOrWhiteSpace(_userInfo.DisplayName) ? _userInfo.Username : _userInfo.DisplayName;
        var notification = await _notifications.SendNotification(
            userId: originalSenderId,
            title: "Friend Request Accepted",
            body: $"{accepterName} accepted your friend request.",
            type: NotificationType.Success,
            link: "/social/friends");
        if (!notification.IsValid)
            _logger.LogWarning("Failed to send friend-accepted notification to {UserId}: {Error}",
                originalSenderId, notification.ErrorMessage);

        return true;
    }

    public async Task<bool> TryDeclineFriendRequest(string requestId)
    {
        var request = await _repos.GetRepository<FriendRequest>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.IsAccepted == null && r.ToUserId == _userInfo.UserId);
        if (request == null) return false;
        
        request.Decline();
        return await ProcessFriendRequest(request, null);
    }

    public async Task<bool> TryCancelFriendRequest(string requestId)
    {
        // Sender withdraws their own still-pending outgoing request.
        var request = await _repos.GetRepository<FriendRequest>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(r => r.Id == requestId && r.IsAccepted == null && r.CreatedById == _userInfo.UserId);
        if (request == null) return false;

        //Hard delete request on cancel
        await _repos.GetRepository<FriendRequest>()
            .DeleteAsync(request);
        return true;
    }

    private async Task<bool> ProcessFriendRequest(FriendRequest request, Friend? friend)
    {
        await _repos.BeginTransactionAsync();
        try
        {
            if(friend != null)
                await _repos.GetRepository<Friend>()
                    .AddAsync(friend, saveNow: false);
            
            await _repos.GetRepository<FriendRequest>()
                .UpdateAsync(request, saveNow: false);

            await _repos.SaveChangesAsync();
            await _repos.CommitTransactionAsync();
            return true;
        }
        catch (Exception ex)
        {
            await _repos.RollbackTransactionAsync();
            _logger.LogError(ex, "Failed to {Process} friend request: {RequestId}",
                friend != null ? "accept" : "decline", request.Id);
            return false;
        }
    }


    // ─── Block helpers ───────────────────────────────────────────────────
    // Inlined here (rather than via BlockAndReportService) to keep the read/accept
    // paths self-defending against a block, both directions.

    private async Task<bool> IsBlockedBetween(string otherUserId)
        => await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .AnyAsync(b => (b.BlockedUserId == otherUserId && b.CreatedById == _userInfo.UserId)
                           || (b.CreatedById == otherUserId && b.BlockedUserId == _userInfo.UserId));

    private async Task<HashSet<string>> GetBlockedUserIds(IEnumerable<string> userIds)
    {
        var list = userIds.ToList();
        if (list.Count == 0) return [];

        var blocked = await _repos.GetRepository<BlockedUser>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .Where(b => (b.CreatedById == _userInfo.UserId && list.Contains(b.BlockedUserId))
                        || (list.Contains(b.CreatedById!) && b.BlockedUserId == _userInfo.UserId))
            .Select(b => b.CreatedById == _userInfo.UserId ? b.BlockedUserId : b.CreatedById!)
            .ToListAsync();
        return blocked.ToHashSet();
    }

    #endregion
    
}