# SignalR Design: Syncing Player Profile and Game Page

## Context

Ultimate Monopoly is a companion app to the physical board game, not a replacement. Around the table there is typically:

- **One tablet** running the "game view" (the controller — shows all players, board state, turn info, etc.). The tablet is logged into one user's account, by default the game creator. Anyone physically at the table can tap on it.
- **Each player's phone** running their **player profile view** (their money, cards, end-turn button, etc.). Phones are optional — a player can also open their profile from the tablet.

All views require authentication.

This document covers how SignalR is used to keep these views in sync, starting with the **game setup page**. In-game sync (turns, prompts, money transfers, the live state broadcast) is now designed elsewhere: the prompt wire surface lives in `choice-events.md` §11, and the command pipeline + whole-cache live broadcast in `web-orchestration.md`.

## Architecture overview

> **Updated 2026-05-28 — two hubs, not one.** This section originally described a single `GameHub` for the whole lifecycle and one `game-{gameId}` group. The implementation split it by lifecycle phase. The setup surface below is unchanged in substance; only the hub it lives on and the group name changed.

- **One hub per lifecycle phase**, both deriving from a shared `GameBaseHub`:
  - **`GameSetupHub`** — the setup page (this document).
  - **`GamePlayHub`** — in-progress play (prompts, live state broadcast — see `choice-events.md` / `web-orchestration.md`).
  - (A separate `PresenceHub` covers online presence, outside the per-game flow.)
- **One group per game *per hub*.** `GameBaseHub.GroupName(prefix, gameId)` => `{prefix}__{gameId}`, so setup clients join `game-setup__{gameId}` and play clients join `game-play__{gameId}`. Keeping them separate means setup and in-play broadcasts never cross. Every connected client (tablet + phones) for a given phase joins that phase's group.
- `GameBaseHub.OnConnectedAsync` reads `gameId` from the connection query string, verifies membership via `GameService.CheckUserInGame`, and aborts the connection if the caller isn't in the game. User identity comes from `Context.UserIdentifier`.
- Authorization for tablet-only actions = caller must be `game.CreatorId` (the host).

## Game setup page

The setup page (tablet) is split:

- **Left:** ordered list of players in the game, each showing their two dice numbers. Order reflects physical seating; creator can drag-reorder.
- **Right:** QR code encoding the join URL for this game.

### Game numbers

Each player rolls two physical dice to determine turn order. The two values (`dice1`, `dice2`) are entered either:

- By the player on their phone, or
- By whoever is at the tablet, on that player's behalf.

The rules for how these numbers determine turn order live in the game logic, not SignalR.

### Join flow

1. Player scans QR code on their phone.
2. QR resolves to `/games/{gameId}/join`.
3. If not signed in, Identity auth flow runs, then returns to join endpoint.
4. Server validates: game exists, is still in setup, capacity not exceeded, user not already joined (idempotent if they are).
5. Server adds `PlayerGame` record and redirects phone to that user's player-profile page for the game.
6. Player-profile page opens a SignalR connection to `GameSetupHub`, which adds the connection to the `game-setup__{gameId}` group and broadcasts `PlayerJoined` to the group.

### SignalR surface (setup phase) — `GameSetupHub`

**Server → client events**

| Event | Payload | Sent to |
|---|---|---|
| `PlayerJoined` | `{ userId, displayName, dice1?, dice2? }` | Group (tablet sees new player in list) |
| `PlayerLeft` | `{ userId }` | Group |
| `PlayerDiceSet` | `{ userId, dice1, dice2 }` | Group |
| `SeatOrderChanged` | `{ orderedUserIds: [] }` | Group |
| `GameStarted` | `{ }` | Group (phones transition from waiting room to in-game profile; tablet transitions to game view) |

**Client → server methods**

| Method | Caller | Auth check |
|---|---|---|
| `JoinGame(gameId)` | Phone post-scan | Authed user, game in setup, capacity ok |
| `SetDiceNumbers(targetUserId, dice1, dice2)` | Phone (self) or tablet (any player) | `caller == targetUserId` OR `caller == game.CreatorId` |
| `ReorderSeats(orderedUserIds[])` | Tablet | `caller == game.CreatorId` |
| `StartGame()` | Tablet | `caller == game.CreatorId`; all players have dice numbers set |

### Phone behaviour during setup

Phones do **not** need live updates of the player list, seat order, or other players' dice numbers during setup. The only event a phone consumes in setup is `GameStarted`, which transitions it from a "you've joined, waiting for host to start" screen into the in-game player-profile view.

The tablet consumes all setup events to keep the player list live.

## Deferred / open items

- ~~**In-game sync** (turn flow, money transfers, cards, board state).~~ Designed — `GamePlayHub` carries it: prompts via `choice-events.md` §11, the whole-cache live broadcast and command pipeline via `web-orchestration.md`. `GameStarted` is the seam: phones drop the setup connection and reconnect to `game-play__{gameId}`.
- **Disconnect / reconnect behaviour** — phones losing connection mid-game. (`GamePlayHub.GetCurrentPrompt` + `GetBoard` cover the reconnect *pull*; the broader policy is still open.)
- **Late joins / spectating** — can a user scan the QR after the game has started?
- **Kicking a player** during setup — UI and event (`PlayerLeft` covers the wire, but the action is not yet specified).
- **Multiple host views** — what happens if the creator opens the game view on two devices simultaneously? Probably fine (both join the group), but worth confirming.


---

# Screen Design

--------------------------------------
| [Game Name / Header]               |
| [Info about the game (rounding, board)] |
| -----------------------------------|
| [Player]         |   [QR CODE (centred)]  |
| [Player]         |                 |
| [Player, etc]    |                 |
| -----------------------------------|
| [Start Game] | [Cancel Game] |
--------------------------------------


Player Cards:
----------------------------------
| [Profile Circle] [Display Name] | [Kick]|
| [Dice Numbers] ("Not Set" if null) |
---------------------------------------

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", "deferred", or "open item" note above may be out of date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.

