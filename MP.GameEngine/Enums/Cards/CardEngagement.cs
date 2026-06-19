namespace MP.GameEngine.Enums.Cards;

/// <summary>
/// How a played card was engaged, collapsed from <see cref="CardConditionType"/> for stats: the two
/// forced conditions (<see cref="CardConditionType.MetCardholderTurn"/> /
/// <see cref="CardConditionType.MetAnyPlayerTurn"/>) fold to <see cref="Forced"/>, the two choice
/// conditions to <see cref="Choice"/>, and a resolve-on-draw card (<see cref="CardConditionType.None"/>)
/// to <see cref="ResolveOnDraw"/>. Used for the "most common engagement of played cards" stat.
/// </summary>
public enum CardEngagement
{
    /// <summary>A held card that fired automatically when its trigger met (the two <c>Met*</c> conditions).</summary>
    Forced,

    /// <summary>A held card the holder chose to play (the two <c>Choice*</c> conditions).</summary>
    Choice,

    /// <summary>A resolve-on-draw card (no held condition) — applied the moment it was drawn.</summary>
    ResolveOnDraw
}