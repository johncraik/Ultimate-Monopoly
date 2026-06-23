
# HOST Play Page (/Game/Play/{gameId})

**Status:** partly built. The live game-state broadcast this view consumes is in
place (`IEngineNotifier.StateChanged`), and the mobile player profile reused in
its drawer (`/player-profile/{gameId}/{userId}`) is built; the host play page
itself is described here as a design and is being built out. See the drift note
at the foot of this document.

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



---

---

# Player Profile Partial

This is the player profile drawer that appears when a player clicks on their
profile card; or when viewing their profile on their phone (as the logged in user, not the host).

The page will be split into "pages", aka tabs - but with a unique design: navigation on the bottom, and a clear nav bar.
This nav bar will be like the main nav bar (glassy and sticky); buttons will be ICONS only, no text.

Pages:
- Profile/Main (this is basic info)
- Properties (details out every property, plus build/sell/mortgage/unreserve actions)
- Cards (details out every card the player has, plus actions to use them if applicable - JUST MOCK FOR NOW!)
- Loans (details out every loan they have, what their loan cost is (when passing go/FP), and command to take out a new loan)
- Deal (leave stub) - no design or functionality yet

Every page has a header contianing:
- Profile card:
  - Player profile card (like in Setup)
  - Shows the player profile circle.
  - Shows their display name
  - Shows their dice number
  - Show they money
- Under profile:
  - Shows full width button for roll/end turn (like we have on host page), hidden when not their turn
  - When NOT their turn, shows whose turn it is (like in host page), but just profile circle + name (no dice nums or money)

## Profile Page

If applicable, at the top:
- Button to leave jail (pay)
- Button to leave jail (card - if they have leave jail card, for now just always show IF in jail)

1st Card/Block:
- Shows the player's money (yes, repeat it with an icon too)
- Shows the player's jail cost
- Shows the player's triple bonus value
- Shows the player's %cap for percent cards (just mock for now, ill make a func for it)

2nd Card/Block:
- List of property sets (types) that have been handed into free parking
- Shown as a badge list (single column, full width badges, text centered)
- Collapsable list (accordian), shows count of types in header

3rd Card/Block:
- Button to declare bankruptcy


## Properties Page

1st Card/Block:
- Shows mortgage fee (if applicable)
- Shows the number of houses and number of hotels left
- Shows total cost to build on ALL available properties (MOCK, func to come later)
- Button to build on all properties (MOCK, func to come later)

2nd Card/Block:
- Accordian style list of properties, grouped by set type:
  - Header is set type + number of properties they own in that set
  - Only shows accordians for set types where they have at least 1 property
  - Header has special SET badge if they own all properties in that set
  - Shown in the accordian are space rectangles (like on the board view), with coloured header, then shown in the body of each space rect:
    - Mortgage value, if not reserved, (or value to UN-mortgage if mortgaged)
    - UN-reserve value if reserved
    - button to BUILD house/hotel/double-hotel if applicable
    - button to SELL house/hotel/double-hotel if applicable
    - button to Mortgage property (if not reserved, and no buildings)
  - In each body, above list of owned properties, is:
    - Build cost + double hotel cost (build cost * 5), if applicable
    - Button to build on each property (if applicable), so itll put a house on every property in the set

## Cards Page

This is defered for now, just put a blank page with "You have no cards" bootstrap-card.

## Loans Page

1st Card/Block:
- Button to take out a new loan (if applicable, can only have 3 loans at once)
- Shows the player's current loan value (if applicable)
- Shows the player's loan cost (if applicable)
- Button to page custom loan amount (if applicable), can pay off any amount off loans at any time, always pays off first loan first when custom

2nd Card/Block:
- List table of loans
- Shows what they took out, what theyve paid, and whats outstanding

## Deal Page

Stub for now, just a blank with "coming soon"

---

## Implementation status & drift

> This document records the **agreed design**, not the live state of the code.
> Since it was written the implementation has moved on — much of what is
> described here is built, and some details have changed. Any status, "TODO",
> "not yet built", or "pre-implementation" note above may be out of date.
>
> Where this doc and the code disagree, the **code (and the developer) win**
> (`docs/development/README.md`). Verify specifics against the current code.

