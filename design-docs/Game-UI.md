
# HOST Play Page (/Game/Play/{gameId})

**Status:** design, pre-implementation. This is the in-game tablet/host view. It
is the concrete consumer that requires a **live game-state projection** (a
per-tablet state broadcast — not yet built) to drive the board, the current-player
quick-stats pane, the Free Parking centre, and the card-% list. The mobile player
profile it reuses inside its drawer (`/player-profile/{gameId}/{userId}`) is built.

## Page Split
- Left: 'Sidebar' - col-4
- Right: 'Game View' - col-8
- Potentially, a "swipe from the right" sidebar that has a "Refresh/Reset" button (if game hangs, something goes wrong, allow manual rollback to latest snapshot and refresh pages); as well as link to rules??

## Sidebar (col-4)
- Single column
- Two rows (50/50 split)
- Top ROW:
  - This is the player list view
  - Shows a list of player profile cards, name + dice numbers
  - Right side of card is a button to "View", which opens their player profile page within the host page in over-the-page sidebar (adding backdrop to entire page)
  - Bankrupted players are grayed out with disabled view button (and clear "Bankrupt" Stamp)
- Bottom ROW:
  - This is the CURRENT player quick info pane
  - Shows quick stats like:
    - Any alerts ("you have loans", "you have mortgaged props", etc)
    - How much money they have
    - How many properties they have (possible broken down by state)
    - How many SETS they have
    - Their triple bonus value
    - Their jail cost value

## Game View (col-8)
- This is the main game view
- Top shows whos turn it is, with "End Turn" button (disabled until valid, aka EndOfTurn State)
- Below is the main game view, in 2 columns:
  - Left: 'Board View' - FIXED width of components height (ENSURE ITS A SQUARE!)
  - Right: 'Game Info' - width fills whatever isnt taken by the board view
- When an interuptable window prompt is active, this section will have a backdrop, and the modal will be above this side of the page, left sidebar remains active

### Board View (SQUARE)
- This will show a virtual board, ideally looking exactly like the real board
- Just colour spaces (no names, no costs, etc): white rectangle, with top banner being prop colour -- KNOWN as a 'Space Rectangle'
- Clicking one will bring up a tooltip with details (name, price, rent level, owner, etc)
- The 'white' section of a space rectangle will be colour coded (transparent colours, so its not too colourful) based on below:
  - Available: lime green
  - Owned: Red
  - Mortgaged: Orange
  - Reserved: light blue
  - In Free Parking: purple
- None-property space rectangles wont have hue colour shading
- Each player will be placed on the board with their profile circle (plus arrow direction) on the space they occupy
- The centre will show how much money is in free parking + the LAST property handed into free parking (there can be more than one property in free parking)
- This section of the page is supposed to be a nice visual representation of the game board; with info

### Game Info (FILL)
- This complements the board view, by condensing information down and giving more detail.
- At the top are banner alerts (some cards grant things like "no rents on stations") - these are global alerts, not player alerts
- Below the alert banners is an accordian list
- Shown in accordian list:
  - Free Parking (show the money + ALL properties in free parking)
  - Available Properties (show ALL properties that are not owned)
  - Number of NOPE cards left
  - Whether reservation rule is active
  - Then each player's %cap (%cards - double hotel=100%, any houses/hotels=50%, none=10%), shown in a list (ACTIVE players only)


## Player Profile Sidebar
- Opens a sidebar from the left, emulating a mobile phone in size, while also adding a backdrop to the rest of the page.
- Can switch between player profiles from a nav header (previous/next player), and close it with a close button.
- Design of player profile page not decided, but premise is it will provide full functionality for an individual player


## Prompts & Actions — Surfaces & Host Authority

### The host is the controller; phones are optional
The host tablet is the authoritative game controller and can perform **every**
action and answer **every** prompt on any player's behalf. Phones (each logged in
as a real player) are an optional convenience layer — useful at a big table where
a player can't reach or see the tablet — not a requirement. Anything a player can
do on their own phone, the host can do for them by opening that player's profile
drawer; the drawer renders the same `/player-profile` view either way.

### Where prompts surface
- **Player prompts** (`DiceRoll`, `AcquireProperty`, `AuctionBid`, `Shortfall`,
  `Acknowledge`, `TargetPlayer`, `TargetProperty`, `CardOption`) render **only on
  the player profile** — i.e. on that player's phone, or inside the host's
  player-profile drawer. They never appear on the game view.
- **The interruptible (NOPE) window** is the one prompt that renders over the
  **game view** — it is host-authority (the host taps Continue, or plays an
  eligible holder's card on their behalf). The left sidebar stays live; only the
  game-view side takes the backdrop. See `choice-events.md` §9. (Contrast the two
  overlays: the profile drawer dims the *whole* page; the interruptible window
  dims only the game-view column.)

### Authority — host-bypass is universal
Authorisation everywhere is "the named player **or** the host":
- **Prompt responses** already enforce this in `PromptValidator` (host-bypass per
  response variant).
- **Commands** must do the same. Today the turn-state command gates
  (`TurnStateProvider.CanPortfolioCommand`, `CanDeal`, `CanLeaveJail`,
  `CanEndTurn`, `CanDeclareBankruptcy`) authorise the *named player only* — they
  need a host-bypass so the host can drive End Turn, deals, jail exit, portfolio
  actions, and voluntary bankruptcy from a player's drawer. This is the engine
  change this page depends on.


## Traceability
1. **`signalr-design.md`** — the tablet/phone sync model and the `game-play`
   group this page connects to.
2. **`choice-events.md`** — the prompt framework; §9 (interruptible window) is the
   only prompt that surfaces on the game view, the rest on the profile.
3. **`turn-state.md`** — the command gates that need the universal host-bypass
   described above.
4. **`game-rules.md`** — the rules behind the surfaced data (percentage-card
   tiers, Free Parking, reserved properties, jail cost escalation).
5. **`/player-profile/{gameId}/{userId}`** — the mobile player profile reused by
   the host's profile drawer.

