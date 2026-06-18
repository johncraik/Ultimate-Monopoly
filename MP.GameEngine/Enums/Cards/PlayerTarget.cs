namespace MP.GameEngine.Enums.Cards;

/// <summary>Who a card action acts on. Shared across movement, jail, and other actions.</summary>
public enum PlayerTarget
{
    /// <summary>The card holder.</summary>
    Self,
    /// <summary>A player the holder chooses (resolved via a TargetPlayer prompt).</summary>
    ChosenPlayer,
    /// <summary>Every other (non-bankrupt) player.</summary>
    AllOthers,
    /// <summary>Every (non-bankrupt) player, the holder included.</summary>
    Everyone,                                                                                                                           
    /// <summary>                                                                                                                       
    /// The winner of the action's dice-off — resolved by <c>MoneyActionService</c> via <c>DiceService</c>                              
    /// (e.g. "the lowest roller pays the tax"). The resolved player is stashed on the shared                                           
    /// <c>CardActionContext</c> so a later action in the group (a Swap) can act on the same one.                                       
    /// </summary>                                                                                                                      
    DiceOffPlayer,
    /// <summary>
    /// The player an earlier action in the same group stashed on <c>CardActionContext.ContextPlayerId</c>
    /// (a dice-off winner or a swap partner) — acted on with no fresh prompt/roll. Used by the GO swap's
    /// "both players receive £200": the Swap stashes the chosen partner, then a money grant pays them.
    /// </summary>
    ContextPlayer,
    /// <summary>
    /// The nearest other player ahead of the holder on the board (scanning in the holder's travel
    /// direction), preferring one also travelling that direction, else the nearest ahead in any direction
    /// (cards-dev-changes.md §4). Board-relative, no prompt — used by the FP "ID check" swap.
    /// </summary>
    NearestPlayerAhead
}