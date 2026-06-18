namespace MP.GameEngine.Enums.Cards;

/// <summary>What a <c>CardTransferAction</c> does — move a held card between players' hands.</summary>
public enum CardTransferKind
{
    /// <summary>The holder gives one of their held cards (their choice) to a dice-off roller.</summary>
    Pass,
    /// <summary>The holder takes a chosen card from a chosen player.</summary>
    Steal
}
