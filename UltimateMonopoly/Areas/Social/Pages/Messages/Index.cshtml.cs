using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using UltimateMonopoly.Models.ViewModels.Social;
using UltimateMonopoly.Services.Friends;

namespace UltimateMonopoly.Areas.Social.Pages.Messages;

/// <summary>The Messages page (E1) — DM conversations with friends. Two-pane: a conversation list (left) and
/// the open conversation (right). New chats are started from a friend's profile (the <c>friendId</c> query).</summary>
[Authorize]
public class IndexModel : PageModel
{
    private readonly FriendMessagingService _messaging;

    public IndexModel(FriendMessagingService messaging) => _messaging = messaging;

    public List<ConversationListItem> Conversations { get; private set; } = [];
    public ConversationView? Active { get; private set; }
    public string? Notice { get; private set; }

    public async Task<IActionResult> OnGetAsync(string? friendId)
    {
        Conversations = await _messaging.GetConversations();

        if (string.IsNullOrEmpty(friendId))
            return Page();

        // Arrived from a profile "Message" button — open/create that DM and pre-select it.
        var (threadId, error) = await _messaging.OpenOrCreate(friendId);
        if (error != null) { Notice = error; return Page(); }

        var (view, loadError) = await _messaging.LoadThread(threadId!);
        if (loadError != null) { Notice = loadError; return Page(); }
        Active = view;

        // A brand-new (empty) conversation won't be in the list yet — surface it so it's selectable.
        if (Active != null && Conversations.All(c => c.ThreadId != Active.ThreadId))
        {
            Conversations.Insert(0, new ConversationListItem
            {
                ThreadId = Active.ThreadId,
                FriendUserId = Active.FriendUserId,
                Friend = Active.Friend,
                Preview = null,
                LastActivityUtc = DateTime.UtcNow,
                Unread = false
            });
        }

        return Page();
    }

    public async Task<IActionResult> OnGetThreadAsync(string threadId)
    {
        var (view, error) = await _messaging.LoadThread(threadId);
        return view == null ? BadRequest(error ?? "Conversation not found.") : Partial("_Conversation", view);
    }

    public async Task<IActionResult> OnPostSendAsync(string threadId, string message)
    {
        if (string.IsNullOrWhiteSpace(message))
            return BadRequest("Message is empty.");

        var (msg, error) = await _messaging.SendMessage(threadId, message.Trim());
        return msg == null
            ? BadRequest(error ?? "Could not send message.")
            : new JsonResult(new { messageId = msg.MessageId, text = msg.Text, sentAtUtc = msg.SentAtUtc });
    }
}
