# E1 — Friend Messaging (Design)

## Context

E1 adds **direct messaging between friends**, built on **JC.Communication.Messaging** (already
registered; the seven messaging tables are migrated). Today the `Friends/Chat` page is **mock data**
(hard-coded users + dummy messages) and the navbar's "Messages" link is a `#` placeholder — this
feature replaces both with a real, live conversation surface.

The roadmap entry (`v1-roadmap.md` §E1) is "Friend messaging **+ game invites**". This doc scopes
**V1 to messaging only**; game invites over the notifications channel are recorded as deferred (§11).

> The package surface is documented in `docs/pckg-docs/JC.Communication/Messaging-*.md`. This doc only
> covers what we build *on top of* it.

## Decisions (locked)

| Decision | Choice |
|---|---|
| Thread type | **DM only** — no group chats (V1) |
| Thread metadata | **None** — `ChatMetadataService` never used; the header/list render the *other participant's* user profile |
| Delivery | **SignalR live** — a push-only hub delivers new messages in real time |
| Start-a-chat entry point | **Friend profile only** — a "Message" button; the Messages page lists only *existing* conversations |
| Page location | New **`/Social/Messages`** page; the mock `Friends/Chat` is retired |
| Package cleanup jobs | `ActivityLogCleanupJob` / `ReadLogCleanupJob` registered **later**, with the one-shot "register all package background jobs" task (not in this feature) |
| Read receipts | Unread **badges** only (free via the package's `MessageReadLog`); no per-message "seen" display |

## 1. Package configuration

One change to the existing registration (`Program.cs`):

```csharp
builder.Services.AddMessaging<AppDbContext>(o => o.DisableGroups = true); // DM-only
```

`DisableGroups = true` rejects any thread with more than two participants at the package level;
`ImmutableDirectMessageParticipants` (default `true`) already locks DM membership. We use
`ChatThreadService`, `ChatMessageService`, and `MessagingLogService` (the package logs activity +
reads itself). We do **not** use `ChatParticipantService` (no add/remove in a DM) or
`ChatMetadataService` (no metadata).

## 2. Guard seam — `FriendMessagingService`

`Services/Friends/FriendMessagingService.cs` (alongside `FriendService` / `BlockAndReportService` /
`PresenceService`). The package is friend/block/role-blind, so **every** rule lives here — each method
validates, then delegates to the JC service.

| Method | Guards (in order) | Delegates to |
|---|---|---|
| `GetConversations()` → left panel | — (scoped to me) | `ChatThreadService.GetUserChats()`; resolve the *other* participant → profile; drop/flag any now-non-friend or now-blocked thread |
| `OpenConversation(friendUserId)` | friends (`FriendService.AreFriends`) · not blocked (`BlockAndReportService.CheckIfBlocksExist`) | `GetOrCreateDefaultChat(params, [friend])` |
| `LoadThread(threadId)` | I'm a participant (package enforces) | `GetChatModelById(threadId)` (auto-logs read) |
| `SendMessage(threadId, text)` | friends · not blocked · **not `Restricted`** (`_userInfo.IsInRole(AppRoles.Restricted)`) | `ChatMessageService.TrySendMessage(threadId, text, null)` → then SignalR push (§3) |

Guard reuse mirrors the existing social code exactly: `AreFriends`, `CheckIfBlocksExist` (bidirectional),
and the `AppRoles.Restricted` check `FriendService.TrySendFriendRequest` already uses. A guard failure
returns a typed result (`(bool Ok, string? Error)`-style) the page surfaces inline.

**Rendering the "other participant":** a DM's `ChatModel.Participants` has two entries; the non-me one's
`UserId` resolves through `UserService` → `UserProfileViewModel`, rendered with the existing
`<profile-circle user="…" size="…">` tag helper (avatar colour/image + initials) — the same component
the mock page and friends list use. **No thread metadata involved.**

## 3. SignalR — `MessagingHub` (push-only)

A new hub at `/hubs/messaging`, mapped in `Program.cs` beside the others. It does **not** derive from
`GameBaseHub` (that base is per-game); it stands alone like `PresenceHub`, `[Authorize]`, and needs
**no manual groups** — SignalR's `Clients.User(userId)` routes by `Context.UserIdentifier` (already the
mechanism `PresenceHub` relies on). The hub itself is essentially empty (connection lifecycle only);
all pushes originate **server-side from the service** via `IHubContext<MessagingHub>`.

**Send path** (validation never lives in the hub):

```
client send-form ──AJAX POST──▶ OnPostSend ──▶ FriendMessagingService.SendMessage
        │                                            │  (guards + TrySendMessage)
        │◀──── confirmed MessageModel (JSON) ────────┤
                                                     └──▶ IHubContext<MessagingHub>
                                                            .Clients.User(recipientId)
                                                            .SendAsync("ReceiveMessage", payload)
```

The **sender's** initiating tab appends from the AJAX response; the **recipient** receives live via the
hub. (Optionally also push to `Clients.User(senderId)` so the sender's *other* open tabs stay in sync —
nice-to-have.)

**Server → client events**

| Event | Payload | Effect on the recipient |
|---|---|---|
| `ReceiveMessage` | `{ threadId, messageId, senderUserId, message, sentAtUtc }` | If that thread is open → append + auto-scroll; always → bump the left-panel preview/time + unread dot |

No client → server methods in V1 (no typing indicator — see §11).

## 4. Page — `/Social/Messages`

`Areas/Social/Pages/Messages/Index.cshtml(.cs)`. Two-pane, AJAX-driven (no full reloads):

- **Handlers:**
  - `OnGetAsync(string? friendId)` — first paint: the conversation list. If `friendId` is supplied
    (arriving from a profile "Message" button), open/create that DM and pre-select it.
  - `OnGetThreadAsync(string threadId)` — returns the **thread partial** (header + messages) for the
    main panel when a conversation is clicked.
  - `OnPostSendAsync(string threadId, string message)` — the send path (§3); returns the confirmed
    `MessageModel` as JSON.
- **Left panel:** conversation rows (friend `profile-circle` + display name + last-message preview +
  relative time + unread dot), newest-activity first. Server-rendered first, live-updated.
- **Main panel:** empty state until a row is selected.
  - **Header:** friend `profile-circle` + display name + **View profile** (→ existing
    `/Social/Friends/Profile/{userId}`, itself friends+block-gated).
  - **Body:** scrollable message list (oldest→newest, auto-scroll to bottom).
  - **Footer:** textarea + send (disabled with a notice for `Restricted` users).
- **JS:** `scripts/Social/messages.js` — opens the `MessagingHub` connection (reusing
  `scripts/SignalR/signalr.min.js`), wires click-to-load, send-on-submit, auto-scroll, the
  `ReceiveMessage` listener, and unread clearing on open. Follows the app's per-feature JS convention.

## 5. Entry point, nav, retire mock

- **"Message" button** on `Friends/Profile` (and friend rows already link there) → `/Social/Messages?friendId={id}`.
- The navbar's existing **`<a href="#">Messages</a>`** placeholder (`Pages/Shared/_Navbar.cshtml`, Friends
  dropdown) points to `/Social/Messages`.
- **Retire** the mock `Areas/Social/Pages/Friends/Chat.*` (remove, or redirect to the new page).

## 6. Restricted & blocking behaviour

- **`Restricted`:** can open and read conversations; the footer is disabled with "your account is
  restricted" and a crafted send is rejected server-side (the A2 boundary — already declared, now
  enforced).
- **Blocking:** a block hides the conversation from the list and rejects opens/sends both directions
  (`CheckIfBlocksExist` is bidirectional). Matches how the friends list / profile already behave.

## 7. Screen design

```
 ┌───────────────────────┬─────────────────────────────────────────────┐
 │  Messages             │  (●) Alice Smith            [ View profile → ]│   ← header
 │ ┌───────────────────┐ ├─────────────────────────────────────────────┤
 │ │(●) Alice    2m   ● │ │                                             │
 │ │  "see you then!"   │ │     Hey, up for a game tonight?    (me) ▸    │
 │ ├───────────────────┤ │                                             │
 │ │(●) Bob      1h     │ │  ◂ (them)  Yeah! 8pm?                        │   ← scrollable
 │ │  "gg"              │ │                                             │     body
 │ ├───────────────────┤ │     Perfect, see you then!        (me) ▸     │
 │ │(●) Carol    3d     │ │                                             │
 │ │  "thanks"          │ │                                             │
 │ └───────────────────┘ ├─────────────────────────────────────────────┤
 │                       │ [ type a message…                    ] [Send]│   ← footer
 └───────────────────────┴─────────────────────────────────────────────┘
   left: existing chats     ● = unread        (●) = profile-circle avatar
```

## 8. What the package handles for us

Soft-delete/restore of messages & threads, per-user thread hide (`ThreadDeleted`), read tracking
(`MessageReadLog`, latest-message-only), activity logging (`ThreadActivityLog` — already surfaced in the
admin Messaging log viewer + App-Logs dashboard), `MaxMessageLength` (10000) validation, and the
`ChatModel`/`MessageModel` projections (soft-deleted children excluded; `CanSeeHistory` honoured).

## 9. Performance notes

- `GetUserChats()` **eager-loads every thread's messages** — fine for opening one thread, heavier for
  the left-panel *list*. Acceptable for V1; the flagged follow-up is a lighter "thread summary" query.
- A thread loads its **full** history (`ChatModel`). Fine for V1; "load older messages" pagination is a
  follow-up.

## 10. Build order

1. `DisableGroups = true` on `AddMessaging`.
2. `FriendMessagingService` (+ DI registration) — the guard seam, with a couple of unit-testable guards.
3. `MessagingHub` + map at `/hubs/messaging`.
4. `/Social/Messages` page (handlers + partials) + `messages.js`.
5. Profile "Message" button + navbar link; retire the mock `Friends/Chat`.

## 11. Deferred / open items

- **Game invites** (the "+ game invites" half of roadmap E1) — invite-to-game over
  `NotificationSender` (`NotificationType.Message`/`Info`). Out of this messaging V1; revisit as **E1b**.
- **Group chats** — `DisableGroups` is the single switch to flip later; the UI would need a participant
  picker and the header/list would need real thread metadata.
- **Typing / online indicators** — `PresenceService` already exists; a typing event + online dot on the
  header is a natural follow-up (we chose "live", not "+presence", for V1).
- **History pagination** + the lighter left-panel summary query (§9).
- **Notify on new message** — optionally fire a notification when the recipient has no live connection
  (`PresenceService.IsOnline == false`), so messages aren't missed. Worth considering once invites land.
- **Package cleanup jobs** — `ActivityLogCleanupJob` + `ReadLogCleanupJob` get registered in the
  upcoming "register all package background jobs" pass, not here.

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code. Where this doc and the
> code disagree, the **code (and the developer) win** (`docs/development/README.md`). Verify specifics
> against the current code.