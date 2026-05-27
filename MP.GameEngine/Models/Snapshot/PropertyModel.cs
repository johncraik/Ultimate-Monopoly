using MP.GameEngine.Enums.Properties;

namespace MP.GameEngine.Models.Snapshot;

public class PropertyModel
{
    public string Name { get; set; }
    public ushort BoardIndex { get; set; }
    
    public string? OwnerPlayerId { get; set; }
    public PropertyState State { get; set; }
    
    public RentLevel RentLevel { get; set; }
    
    public StreetRuleQualifier StreetRuleQualifier { get; set; }

    public PropertyModel()
    {
    }

    public PropertyModel(PropertyModel model)
    {
        Name = model.Name;
        BoardIndex = model.BoardIndex;
        
        OwnerPlayerId = model.OwnerPlayerId;
        State = model.State;
        RentLevel = model.RentLevel;
        
        StreetRuleQualifier = model.StreetRuleQualifier;
    }


    #region Property State Methods

    private void UnownedProperty()
    {
        OwnerPlayerId = null;
        RentLevel = RentLevel.SINGLE;
        StreetRuleQualifier = StreetRuleQualifier.None;
    }
    
    public void ReturnToBank()
    {
        State = PropertyState.NotOwned;
        UnownedProperty();
    }

    public void HandInToFreeParking()
    {
        State = PropertyState.FreeParking;
        UnownedProperty();
    }

    public void OwnProperty(string playerId)
    {
        if(OwnerPlayerId != null && OwnerPlayerId == playerId)
            //Cannot own a property that is already owned by the player
            return;
        
        //Determines whether property state changes (mortgaged and reserved properties remain mortgaged/reserved)
        var transferBetweenPlayers = OwnerPlayerId != null;
        
        OwnerPlayerId = playerId;
        StreetRuleQualifier = StreetRuleQualifier.NeverBuiltOn;

        if (!transferBetweenPlayers)
            State = PropertyState.Owned;
    }

    public void MortgageProperty()
    {
        if(OwnerPlayerId == null || State == PropertyState.Reserved)
            return;
        
        State = PropertyState.Mortgaged;
    }
    
    public void ReserveProperty()
    {
        if(OwnerPlayerId == null || State == PropertyState.Mortgaged)
            return;
        
        State = PropertyState.Reserved;
    }
    
    #endregion
}