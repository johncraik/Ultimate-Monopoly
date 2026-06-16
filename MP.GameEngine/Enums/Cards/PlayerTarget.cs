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
    DiceOffPlayer 
}