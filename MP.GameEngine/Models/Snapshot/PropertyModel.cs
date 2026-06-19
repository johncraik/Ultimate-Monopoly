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

    public bool IsPurged { get; set; }
    public bool HasBeenPurged { get; set; }
    
    public bool HasBeenBuiltOnThisTurn { get; set; }
    
    public bool ChargeRent(string playerId) 
        => (OwnerPlayerId != null && OwnerPlayerId != playerId && State == PropertyState.Owned) 
           || (OwnerPlayerId == null && State == PropertyState.FreeParking);
    
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
        
        IsPurged = model.IsPurged;
        HasBeenPurged = model.HasBeenPurged;
        HasBeenBuiltOnThisTurn = model.HasBeenBuiltOnThisTurn;
        
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
        IsPurged = false;
        HasBeenPurged = false;
        StreetRuleQualifier = StreetRuleQualifier.NeverBuiltOn;

        if (transferBetweenPlayers) return;
        
        State = PropertyState.Owned;
    }

    public void MortgageProperty()
    {
        if(OwnerPlayerId == null || State == PropertyState.Reserved)
            return;
        
        State = PropertyState.Mortgaged;
    }
    
    public void UnmortgageProperty()
    {
        if(OwnerPlayerId == null || State != PropertyState.Mortgaged)
            return;
        
        State = PropertyState.Owned;
    }
    
    public void ReserveProperty()
    {
        if(OwnerPlayerId == null || State == PropertyState.Mortgaged)
            return;
        
        State = PropertyState.Reserved;
    }
    
    public void UnreserveProperty()
    {
        if(OwnerPlayerId == null || State != PropertyState.Reserved)
            return;
        
        State = PropertyState.Owned;
    }
    
    public bool BuiltOn() 
        => RentLevel is RentLevel.ONE_HOUSE 
        or RentLevel.TWO_HOUSES 
        or RentLevel.THREE_HOUSES 
        or RentLevel.FOUR_HOUSES 
        or RentLevel.HOTEL 
        or RentLevel.DOUBLE_HOTEL;
    
    #endregion
}