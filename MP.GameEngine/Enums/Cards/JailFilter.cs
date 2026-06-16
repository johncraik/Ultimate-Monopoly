namespace MP.GameEngine.Enums.Cards;

/// <summary>Filters a <c>MovementAction</c>'s resolved targets by jail state.</summary>
public enum JailFilter
{
    /// <summary>No filter — every resolved target moves.</summary>
    None,
    /// <summary>Only players currently in jail ("all prisoners escape …").</summary>
    OnlyJailed,
    /// <summary>Only players not in jail ("all other players not in jail …").</summary>
    OnlyNotJailed
}