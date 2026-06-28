using JC.Communication.Messaging.Models;
using JC.Communication.Messaging.Models.DomainModels;
using JC.Communication.Messaging.Services;
using JC.Core.Models;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Data;
using UltimateMonopoly.Hubs;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Services.Friends;

/// <summary>
/// The E1 guard seam over JC.Communication.Messaging. The package is friend/block/role-blind, so <b>every</b>
/// rule lives here: messaging is <b>friends-only</b>, respects <b>blocking</b> (both directions), and a
/// <c>Restricted</c> account is <b>read-only</b> (can open/read, cannot send). DM-only — threads are created
/// via <see cref="ChatThreadService.GetOrCreateDefaultChat"/> with a single other participant. New messages are
/// pushed live to the recipient through <see cref="MessagingHub"/>.
/// </summary>
public class FriendMessagingService
{
    private readonly ChatThreadService _threads;
    private readonly ChatMessageService _messages;
    private readonly FriendService _friends;
    private readonly BlockAndReportService _blocks;
    private readonly UserService _users;
    private readonly UrlLinkService _urls;
    private readonly IUserInfo _userInfo;
    private readonly IHubContext<MessagingHub> _hub;
    private readonly AppDbContext _context;

    public FriendMessagingService(ChatThreadService threads,
        ChatMessageService messages,
        FriendService friends,
        BlockAndReportService blocks,
        UserService users,
        UrlLinkService urls,
        IUserInfo userInfo,
        IHubContext<MessagingHub> hub,
        AppDbContext context)
    {
        _threads = threads;
        _messages = messages;
        _friends = friends;
        _blocks = blocks;
        _users = users;
        _urls = urls;
        _userInfo = userInfo;
        _hub = hub;
        _context = context;
    }

    // ─── left panel ──────────────────────────────────────────────────────

    /// <summary>Every conversation the current user has with a (still-)friend who isn't blocked, newest first.</summary>
    public async Task<List<ConversationListItem>> GetConversations()
    {
        var me = _userInfo.UserId;
        var chats = await _threads.GetUserChats();
        if (chats.Count == 0) return [];

        var pairs = chats.Select(c => (Chat: c, OtherId: OtherId(c)))
            .Where(x => x.OtherId != null).ToList();
        var otherIds = pairs.Select(x => x.OtherId!).Distinct().ToList();

        var friends = (await _friends.AreFriends(otherIds)).Where(f => f.Firends).Select(f => f.userId).ToHashSet();
        var blocked = (await _blocks.CheckAndReportExistingBlocks(me, otherIds)).Where(b => b.Blocked).Select(b => b.userId).ToHashSet();
        var users = await _users.GetUserDictionary(otherIds);

        // Unread = the thread's latest message is from the other user and we have no read-log for it.
        var lastByThread = pairs.ToDictionary(x => x.Chat.ThreadId, x => x.Chat.Messages.OrderBy(m => m.SentAtUtc).LastOrDefault());
        var unreadCandidateIds = lastByThread.Values.Where(m => m != null && m!.SenderUserId != me).Select(m => m!.MessageId).ToList();
        var readByMe = unreadCandidateIds.Count == 0
            ? new HashSet<string>()
            : (await _context.MessageReadLogs.AsNoTracking()
                .Where(r => r.UserId == me && unreadCandidateIds.Contains(r.MessageId))
                .Select(r => r.MessageId).ToListAsync()).ToHashSet();

        var items = new List<ConversationListItem>();
        foreach (var (chat, otherId) in pairs)
        {
            if (otherId == null || !friends.Contains(otherId) || blocked.Contains(otherId)) continue;
            if (!users.TryGetValue(otherId, out var user)) continue;

            var last = lastByThread[chat.ThreadId];
            var unread = last != null && last.SenderUserId != me && !readByMe.Contains(last.MessageId);
            items.Add(new ConversationListItem
            {
                ThreadId = chat.ThreadId,
                FriendUserId = otherId,
                Friend = new UserProfileViewModel(user, _urls.GetImgUrl(user.AvatarImageName)),
                Preview = last?.Message,
                LastActivityUtc = last?.SentAtUtc,
                Unread = unread
            });
        }

        return items.OrderByDescending(i => i.LastActivityUtc ?? DateTime.MinValue).ToList();
    }

    // ─── open / load / send ──────────────────────────────────────────────

    /// <summary>Get-or-create the DM with a friend (from the profile "Message" button). Returns the thread id.</summary>
    public async Task<(string? ThreadId, string? Error)> OpenOrCreate(string friendUserId)
    {
        var guard = await Guard(friendUserId);
        if (guard != null) return (null, guard);

        var (chat, resp) = await _threads.GetOrCreateDefaultChat(
            new ChatThreadParams(ChatThread.DirectMessageName, description: null), new ChatParticipant(friendUserId));
        return chat == null ? (null, resp.ErrorMessage ?? "Could not open this conversation.") : (chat.ThreadId, null);
    }

    /// <summary>Load a thread's messages for the main panel (logs a read of the latest message).</summary>
    public async Task<(ConversationView? View, string? Error)> LoadThread(string threadId)
    {
        var chat = await _threads.GetChatModelById(threadId);
        if (chat == null) return (null, "Conversation not found.");

        var otherId = OtherId(chat);
        if (otherId == null) return (null, "Conversation not found.");

        var guard = await Guard(otherId);
        if (guard != null) return (null, guard);

        var view = await BuildView(chat, otherId);
        return view == null ? (null, "Conversation not found.") : (view, null);
    }

    /// <summary>Send a message (Restricted → rejected), then push it live to the recipient.</summary>
    public async Task<(MessageView? Message, string? Error)> SendMessage(string threadId, string text)
    {
        if (_userInfo.IsInRole(AppRoles.Restricted))
            return (null, "Your account is restricted and cannot send messages.");

        var chat = await _threads.GetChatModelById(threadId);
        if (chat == null) return (null, "Conversation not found.");

        var otherId = OtherId(chat);
        if (otherId == null) return (null, "Conversation not found.");

        var guard = await Guard(otherId);
        if (guard != null) return (null, guard);

        var resp = await _messages.TrySendMessage(threadId, text, null);
        if (!resp.IsValid || resp.ValidatedChatMessage == null)
            return (null, resp.ErrorMessage ?? "Could not send message.");

        var msg = resp.ValidatedChatMessage;
        await _hub.Clients.User(otherId).SendAsync("ReceiveMessage", new
        {
            threadId,
            messageId = msg.Id,
            senderUserId = _userInfo.UserId,
            message = msg.Message,
            sentAtUtc = msg.SentAtUtc
        });

        return (new MessageView(true, msg.Message, msg.SentAtUtc, msg.Id), null);
    }

    // ─── helpers ─────────────────────────────────────────────────────────

    private string? OtherId(ChatModel chat)
        => chat.Participants.Select(p => p.UserId).FirstOrDefault(id => id != _userInfo.UserId);

    private async Task<string?> Guard(string otherUserId)
    {
        if (!await _friends.AreFriends(otherUserId)) return "You can only message friends.";
        if (await _blocks.CheckIfBlocksExist(_userInfo.UserId, new[] { otherUserId })) return "This conversation isn't available.";
        return null;
    }

    private async Task<ConversationView?> BuildView(ChatModel chat, string otherUserId)
    {
        var user = await _users.GetUserById(otherUserId);
        if (user == null) return null;

        var me = _userInfo.UserId;
        var ordered = chat.Messages.OrderBy(m => m.SentAtUtc).ToList();
        var msgs = ordered.Select(m => new MessageView(m.SenderUserId == me, m.Message, m.SentAtUtc, m.MessageId)).ToList();

        // Read receipt — only when my message is the thread's latest and the friend has read it.
        DateTime? lastReadAtUtc = null;
        var last = ordered.LastOrDefault();
        if (last != null && last.SenderUserId == me)
            lastReadAtUtc = await _context.MessageReadLogs.AsNoTracking()
                .Where(r => r.MessageId == last.MessageId && r.UserId == otherUserId)
                .Select(r => (DateTime?)r.ReadAtUtc)
                .FirstOrDefaultAsync();

        return new ConversationView
        {
            ThreadId = chat.ThreadId,
            FriendUserId = otherUserId,
            Friend = new UserProfileViewModel(user, _urls.GetImgUrl(user.AvatarImageName)),
            Messages = msgs,
            CanSend = !_userInfo.IsInRole(AppRoles.Restricted),
            LastReadAtUtc = lastReadAtUtc
        };
    }
}
