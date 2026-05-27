namespace MP.GameEngine.Enums.Properties;

public enum StreetRuleQualifier
{
    /// <summary>
    /// Default state. Used for un-owned and FP properties.
    /// </summary>
    None,
    
    /// <summary>
    /// Used for properties owned by a player that have not been built on since ownership.
    /// </summary>
    NeverBuiltOn,
    
    /// <summary>
    /// Used for properties owned by a player that have been built on since ownership.
    /// </summary>
    BuiltOn,
    
    /// <summary>
    /// Used for properties in a street where the owning player has qualified for the street rule.
    /// These properties may have or not have been built on; but the street rule is still active.
    /// </summary>
    Qualified
}