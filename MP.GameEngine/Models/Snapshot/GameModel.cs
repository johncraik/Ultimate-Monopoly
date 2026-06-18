using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Models.Snapshot;

public class GameModel
{
    public string GameId { get; set; }
    
    /// <summary>
    /// This stores information about the turn that can be traced back to database models
    /// </summary>
    public TurnMetadata Metadata { get; set; }

    public bool ReserveRuleActive { get; set; }
    public uint FreeParkingAmount { get; set; }
    
    /// <summary>
    /// Global event info that impacts specific spaces/rules in the game
    /// </summary>
    public EventInfo GlobalEventInfo { get; set; } = new();
    
    /// <summary>
    /// The modified dice roll type from convert/downgrade cards
    /// </summary>
    public DiceRollType? ModifiedDiceRollType { get; set; }
    
    
    /// <summary>
    /// The array of players in the game
    /// </summary>
    public List<PlayerModel> Players { get; set; } = [];

    /// <summary>
    /// Properties owned by the bank and available for purchase
    /// </summary>
    public List<PropertyModel> Properties { get; set; } = [];

    /// <summary>
    /// Card decks (for each card type) that are not owned by any player
    /// </summary>
    public CardListModel CardDecks { get; set; } = new();


    public GameModel()
    {
    }

    public GameModel(GameModel model)
    {
        GameId = model.GameId;
        Metadata = new TurnMetadata(model.Metadata);
        
        ReserveRuleActive = model.ReserveRuleActive;
        FreeParkingAmount = model.FreeParkingAmount;
        GlobalEventInfo = new EventInfo(model.GlobalEventInfo);
        ModifiedDiceRollType = model.ModifiedDiceRollType;
        
        Players = model.Players.Select(p => new PlayerModel(p)).ToList();
        Properties = model.Properties.Select(p => new PropertyModel(p)).ToList();
        CardDecks = new CardListModel(model.CardDecks);
    }


    #region Player Primitive Methods

    /// <summary>
    /// Retrieves the current player from the game based on their unique player ID, optionally filtering
    /// based on their bankruptcy status.
    /// </summary>
    /// <param name="bankrupt">
    /// Indicates whether to retrieve the current player based on their bankruptcy status. If true, only a bankrupt
    /// player will be returned. If false, only a non-bankrupt player will be returned.
    /// </param>
    /// <returns>
    /// A <see cref="PlayerModel"/> representing the current player that matches the given bankruptcy status.
    /// If no matching player is found, null is returned.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the current player ID does not match any player in the game's player list.
    /// </exception>
    public PlayerModel? CurrentPlayer(bool bankrupt = false)
        => GetPlayer(Metadata.CurrentPlayerId, bankrupt);

    /// <summary>
    /// Retrieves a player from the game based on their unique player ID, optionally filtering
    /// based on their bankruptcy status.
    /// </summary>
    /// <param name="playerId">
    /// The unique identifier of the player to retrieve.
    /// </param>
    /// <param name="bankrupt">
    /// Indicates whether to retrieve the player based on their bankruptcy status. If true, only a bankrupt
    /// player with the specified ID will be returned. If false, only a non-bankrupt player with the specified ID
    /// will be returned.
    /// </param>
    /// <returns>
    /// A <see cref="PlayerModel"/> representing the player that matches the given player ID and bankruptcy status.
    /// If no matching player is found, null is returned.
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when no player with the specified ID is found in the game's player list.
    /// </exception>
    public PlayerModel? GetPlayer(string playerId, bool bankrupt = false)
    {
        var player = Players.FirstOrDefault(p => p.PlayerId == playerId);
        if (player == null) throw new InvalidOperationException($"Player {playerId} not found in game players list.");

        return player.IsBankrupt == bankrupt ? player : null;
    }


    /// <summary>
    /// Retrieves a list of players currently participating in the game, optionally filtering
    /// based on their bankruptcy status or excluding the player in the point-of-view.
    /// </summary>
    /// <param name="bankrupt">
    /// Indicates whether to include players in a bankrupt state. If false, bankrupt players
    /// will be excluded from the result.
    /// </param>
    /// <param name="excludePovPlayer">
    /// Indicates whether to exclude the player in the current point-of-view from the list.
    /// If true, the POV player will not be included in the returned list.
    /// </param>
    /// <returns>
    /// A list of <see cref="PlayerModel"/> objects representing the players that meet the given criteria.
    /// </returns>
    public List<PlayerModel> GetPlayers(bool bankrupt = false, bool excludePovPlayer = true)
        => GetPlayers(Metadata.CurrentPlayerId, bankrupt, excludePovPlayer);

    /// <summary>
    /// Retrieves a list of players currently participating in the game, sorted relative to the specified
    /// point-of-view player. Players can be filtered based on their bankruptcy status and optionally exclude
    /// the point-of-view player from the result.
    /// </summary>
    /// <param name="povPlayerId">
    /// The ID of the point-of-view player used to determine the relative ordering within the returned list.
    /// </param>
    /// <param name="bankrupt">
    /// Indicates whether to include players in a bankrupt state. If false, bankrupt players
    /// will be excluded from the result.
    /// </param>
    /// <param name="excludePovPlayer">
    /// Indicates whether to exclude the point-of-view player from the returned list. If true,
    /// the point-of-view player will not be included.
    /// </param>
    /// <returns>
    /// A list of <see cref="PlayerModel"/> objects representing the players matching the specified criteria,
    /// ordered based on their relative positioning to the point-of-view player.
    /// </returns>
    public List<PlayerModel> GetPlayers(string povPlayerId, bool bankrupt = false, bool excludePovPlayer = true)
    {
        var players = Players.Where(p => p.IsBankrupt == bankrupt).ToList();
        var povPlayer = GetPlayer(povPlayerId, bankrupt);
        if (povPlayer == null)
        {
            povPlayer = GetPlayer(povPlayerId, !bankrupt);
            if(povPlayer == null)
                return players.OrderBy(p => p.OrderId).ToList();
            
            //Could not find POV player with bankrupt filter, but found when flipping bankrupt filter
            //Therefore, always exclude POV player from the list (as they do not match bankrupt filter)
            excludePovPlayer = true;
        }
        
        var afterPov = players.Where(p => p.OrderId > povPlayer.OrderId)
            .OrderBy(p => p.OrderId);
        var beforePov = players.Where(p => p.OrderId < povPlayer.OrderId)
            .OrderBy(p => p.OrderId);
        
        var list = new List<PlayerModel>(afterPov);
        if(!excludePovPlayer) list.Insert(0, povPlayer);
        
        list.AddRange(beforePov);
        return list;
    }


    public string? CheckAnyDiceNumbers(DiceRoll roll)
    {
        var players = GetPlayers(excludePovPlayer: false);
        return players.FirstOrDefault(p => p.IsDiceNumber(roll))?.PlayerId;
    }
    
    
    public ushort PlayerPercentCap()
        => PlayerPercentCap(Metadata.CurrentPlayerId);

    public ushort PlayerPercentCap(string playerId)
    {
        var owned = GetOwnedProperties(playerId);
        var hasDoubleHotel = owned.Any(p => p.RentLevel == RentLevel.DOUBLE_HOTEL);
        var hasBuildings = owned.Any(p => p.RentLevel is > RentLevel.SET and < RentLevel.DOUBLE_HOTEL);
        return (ushort)(hasDoubleHotel ? 100 : hasBuildings ? 50 : 10);
    }

    #endregion


    #region Property Primitive Methods

    /// <summary>
    /// Retrieves the property located at a specific board index within the current game.
    /// </summary>
    /// <param name="boardIndex">
    /// The index of the board position to lookup. Represents the location of the property on the game board.
    /// </param>
    /// <returns>
    /// A <see cref="PropertyModel"/> representing the property at the specified board index if found,
    /// or null if no property exists at that location.
    /// </returns>
    public PropertyModel? GetPropertySpace(ushort boardIndex)
        => Properties.FirstOrDefault(p => p.BoardIndex == boardIndex);


    /// <summary>
    /// Retrieves a list of properties owned by the current player, allowing for optional filtering
    /// based on property sets, mortgaged status, and reserved status.
    /// </summary>
    /// <param name="set">
    /// The property set to filter by. If provided, only properties belonging to the specified set
    /// will be included. If null, properties from all sets are included.
    /// </param>
    /// <param name="includeMortgaged">
    /// Indicates whether mortgaged properties should be included in the result. If true, mortgaged
    /// properties are included; if false, they are excluded.
    /// </param>
    /// <param name="includeReserved">
    /// Indicates whether reserved properties should be included in the result. If true, reserved
    /// properties are included; if false, they are excluded.
    /// </param>
    /// <returns>
    /// A list of <see cref="PropertyModel"/> objects representing the properties owned by the current
    /// player, with the specified filtering applied.
    /// </returns>
    public List<PropertyModel> GetOwnedProperties(PropertySet? set = null, bool includeMortgaged = true,
        bool includeReserved = true)
        => GetOwnedProperties(Metadata.CurrentPlayerId, set, includeMortgaged, includeReserved);

    /// <summary>
    /// Retrieves a list of properties owned by the current player, filtered by optional criteria such as property set,
    /// mortgage status, and reserved status.
    /// </summary>
    /// <param name="playerId">The ID of the player whose properties are to be retrieved.</param>
    /// <param name="set">
    /// An optional <see cref="PropertySet"/> to filter the owned properties by a specific set of properties (e.g., Brown, Blue, etc.).
    /// If null, no filtering is applied based on property set.
    /// </param>
    /// <param name="includeMortgaged">
    /// A boolean indicating whether mortgaged properties should be included in the results. If true, mortgaged properties are included;
    /// otherwise, they are excluded.
    /// </param>
    /// <param name="includeReserved">
    /// A boolean indicating whether reserved properties should be included in the results. If true, reserved properties are included;
    /// otherwise, they are excluded.
    /// </param>
    /// <returns>
    /// A list of <see cref="PropertyModel"/> objects representing the properties owned by the current player,
    /// filtered according to the specified criteria.
    /// </returns>
    public List<PropertyModel> GetOwnedProperties(string playerId, PropertySet? set = null,
        bool includeMortgaged = true, bool includeReserved = true)
    {
        var properties = Properties
            .Where(pr => !string.IsNullOrEmpty(pr.OwnerPlayerId) && pr.OwnerPlayerId == playerId)
            .OrderBy(p => PropertySetHelper.ResolveSet(p.BoardIndex))
            .ThenBy(p => p.BoardIndex)
            .ToList();
        
        if (!includeMortgaged) 
            properties = properties.Where(pr => pr.State != PropertyState.Mortgaged).ToList();
        
        if (!includeReserved) 
            properties = properties.Where(pr => pr.State != PropertyState.Reserved).ToList();
        
        if(set != null)
            properties = properties.Where(pr => PropertySetHelper.GetIndexes((PropertySet)set).Contains(pr.BoardIndex))
                .ToList();
        
        return properties;
    }

    
    public List<PropertyModel> TradableProperties(PropertySet? set = null, bool includeMortgaged = false)
        => TradableProperties(Metadata.CurrentPlayerId, set, includeMortgaged);
    
    public List<PropertyModel> TradableProperties(string playerId, PropertySet? set = null, bool includeMortgaged = false)
    {
        //Tradable properties are ones that can be traded/mortgaged/handed into free parking
        var properties = GetOwnedProperties(playerId, set, includeMortgaged, false);
        
        var validProps = new List<PropertyModel>();
        foreach (var p in properties)
        {
            if(p.BuiltOn())
                continue;
            
            var pSet = PropertySetHelper.ResolveSet(p.BoardIndex);
            if (pSet == null)
                continue;
            
            if(pSet == PropertySet.Utility 
               || pSet == PropertySet.Station 
               || p.RentLevel == RentLevel.SINGLE)
            {
                validProps.Add(p);
                continue;
            }
            
            var canMortgage = CanMortgageProperty(playerId, p.BoardIndex);
            if (canMortgage)
                validProps.Add(p);
        }

        return validProps;
    }

    public List<PropertyModel> BuiltOnProperties(string playerId, PropertySet? set = null)
    {
        if (set == PropertySet.Station || set == PropertySet.Utility)
            return [];
        
        var properties = GetOwnedProperties(playerId, set);
        if (set != null)
            properties = properties
                .Where(p => PropertySetHelper.ResolveSet(p.BoardIndex) == set)
                .ToList();

        return properties.Where(p => p.BuiltOn()).ToList();
    }


    /// <summary>
    /// Determines if the current player has achieved a "street effect" for the specified property set,
    /// considering built properties and optionally including mortgaged or reserved properties.
    /// </summary>
    /// <param name="set">The property set to evaluate for the street effect.</param>
    /// <param name="absoluteCheck">
    /// A boolean indicating whether to perform an absolute check. If true, mortgaged and reserved properties
    /// are ignored when determining if the street effect has been achieved.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether the current player has achieved the street effect for the
    /// specified property set. If the property set is Station or Utility, the method returns false.
    /// </returns>
    public bool HasStreetEffect(PropertySet set, bool absoluteCheck = true)
        => HasStreetEffect(Metadata.CurrentPlayerId, set, absoluteCheck);
    
    /// <summary>
    /// Determines if a player has achieved a "street effect" for the specified property set,
    /// considering built properties and optionally including mortgaged or reserved properties.
    /// </summary>
    /// <param name="playerId">The unique identifier of the player.</param>
    /// <param name="set">The property set to check for the street effect.</param>
    /// <param name="absoluteCheck">
    /// A boolean indicating whether to perform an absolute check,
    /// ignoring mortgaged and reserved properties if set to true.
    /// </param>
    /// <returns>
    /// A boolean value indicating whether the player has achieved the street effect for the specified property set.
    /// Returns false if the set is Station or Utility.
    /// </returns>
    public bool HasStreetEffect(string playerId, PropertySet set, bool absoluteCheck = true)
    {
        if (!PropertySetHelper.StreetPartner.TryGetValue(set, out var partner))
            //Stations and utilities don't have street effects, and not in street partner dictionary
            return false;
        
        var properties = GetOwnedProperties(playerId, includeMortgaged: !absoluteCheck, includeReserved: !absoluteCheck);
        var streetIndexes = PropertySetHelper.GetIndexes(set).Concat(PropertySetHelper.GetIndexes(partner)).ToHashSet();
        
        //HAS street rule when ALL properties in the set are marked as qualified (set in NormaliseProperties)
        return properties.Count(p => p.StreetRuleQualifier == StreetRuleQualifier.Qualified
                                     && streetIndexes.Contains(p.BoardIndex)) == streetIndexes.Count;
    }


    public void CheckReservationRule(string playerId)
    {
        if(!ReserveRuleActive)
            //Never can be turned back on
            return;

        var players = GetPlayers(playerId);
        var playersWithReservationsCount = players
            .Select(player => GetOwnedProperties(player.PlayerId))
            .Count(props => props.Any(p => p.State == PropertyState.Reserved));

        if (playersWithReservationsCount < players.Count)
            //Not all players have reservations
            return;
        
        //Passed all validation:
        //-Rule is active
        //-All players (except player passed in) have reservations
        //Therefore, rule can be disabled, as last player is "reserving" (breaks reservation deadlock)
        ReserveRuleActive = false;
    }

    /// <summary>
    /// Turns off the reservation rule when <paramref name="playerId"/> has just
    /// obtained a complete colour set outright — e.g. winning it at auction, a
    /// deal, or a Free Parking take. One player breaking through to a full set
    /// ends the mechanic for everyone (game-rules.md Reserved Properties).
    /// Reserved properties don't count (a reservation isn't a completed set);
    /// stations and utilities are never reservable, so they never trigger it.
    /// No-op once the rule is already off — it never turns back on.
    /// </summary>
    public void CheckReservationRuleSetObtained(string playerId)
    {
        if(!ReserveRuleActive)
            return;

        // Reserved excluded (a reservation isn't a completed set); stations and
        // utilities never trigger it (excluded by GetOwnedSets' onlyBuildable).
        // Mortgaged still counts — the player has obtained the set either way.
        var owned = GetOwnedProperties(playerId, includeReserved: false);
        if(PropertySetHelper.GetOwnedSets(playerId, owned).Count > 0)
            ReserveRuleActive = false;
    }

    #endregion



    #region Build on (houses/hotels) Properties Methods

    
    public (ushort HousesTaken, ushort HotelsTaken) GetHousesAndHotelsTaken(string? playerId = null)
    {
        var ownedProperties = string.IsNullOrEmpty(playerId) 
            ? Properties.Where(p => !string.IsNullOrEmpty(p.OwnerPlayerId)).ToList() 
            : GetOwnedProperties(playerId);

        var houseCount = ownedProperties.Count(p => p.RentLevel == RentLevel.ONE_HOUSE);
        houseCount += ownedProperties.Count(p => p.RentLevel == RentLevel.TWO_HOUSES) * 2;
        houseCount += ownedProperties.Count(p => p.RentLevel == RentLevel.THREE_HOUSES) * 3;
        houseCount += ownedProperties.Count(p => p.RentLevel == RentLevel.FOUR_HOUSES) * 4;
        var hotelCount = ownedProperties.Count(p => p.RentLevel == RentLevel.HOTEL);
        hotelCount += ownedProperties.Count(p => p.RentLevel == RentLevel.DOUBLE_HOTEL) * 2;

        return ((ushort)houseCount, (ushort)hotelCount);
    }

    public (ushort HousesLeft, ushort HotelsLeft) GetHousesAndHotelsLeft()
    {
        var (houses, hotels) = GetHousesAndHotelsTaken();
        
        if(houses > RuleDictionary.HouseCount)
            houses = RuleDictionary.HouseCount;
        
        if(hotels > RuleDictionary.HotelCount)
            hotels = RuleDictionary.HotelCount;
        
        return ((ushort)(RuleDictionary.HouseCount - houses), (ushort)(RuleDictionary.HotelCount - hotels));
    }


    private (bool Success, List<PropertyModel> OwnedInSet, PropertyModel? Property) RentLevelBuySellCheck(string playerId, ushort boardIndex)
    {
        var resolvedSet = PropertySetHelper.ResolveSet(boardIndex);
        //Cant buy/sell on utility or station (or null set, aka not a property space)
        if (resolvedSet is null or PropertySet.Utility or PropertySet.Station)
            return (false, [], null);
        
        var set = (PropertySet)resolvedSet;
        var ownedInSet = GetOwnedProperties(playerId, set);
        
        var property = ownedInSet.FirstOrDefault(p => p.BoardIndex == boardIndex);
        //Cant buy/sell on a property that is not owned by the player
        if(property is null)
            return (false, ownedInSet, null);
        
        //Cant buy/sell on the property if you do not own the set
        if(ownedInSet.Count != PropertySetHelper.GetIndexes(set).Count)
            return (false, ownedInSet, property);
        
        //Cant buy/sell on a mortgaged or reserved property; or any property in the set that has a mortgaged or reserved property
        if (ownedInSet.Any(p => p.State is PropertyState.Mortgaged or PropertyState.Reserved))
            return (false, ownedInSet, property);
        
        //Passed all main checks for buy/sell
        return (true, ownedInSet, property);
    }
    
    
    public bool CanIncreaseRentLevel(ushort boardIndex)
        => CanIncreaseRentLevel(Metadata.CurrentPlayerId, boardIndex);
    
    public bool CanIncreaseRentLevel(string playerId, ushort boardIndex)
    {
       var (success, ownedInSet, property) = RentLevelBuySellCheck(playerId, boardIndex);
       if(!success || property == null) return false;
       
       var (housesLeft, hotelsLeft) = GetHousesAndHotelsLeft();
       switch (property.RentLevel)
       {
           //Building a house (SET, 1house, 2houses, 3houses)
           case >= RentLevel.SET and < RentLevel.FOUR_HOUSES when housesLeft == 0:
           //Building a hotel (4houses, Hotel)
           case >= RentLevel.FOUR_HOUSES and < RentLevel.DOUBLE_HOTEL when hotelsLeft == 0:
               return false;
       }

       //Cant build on a property that has already been built on this turn
       if(property.HasBeenBuiltOnThisTurn)
           return false;

        //Cant build more than a double hotel on a property
        if(property.RentLevel == RentLevel.DOUBLE_HOTEL)
            return false;

        //Cant build more double hotels on a property if already at max per set
        if(property.RentLevel == RentLevel.HOTEL 
           && ownedInSet.Count(p => p.RentLevel == RentLevel.DOUBLE_HOTEL) >= RuleDictionary.MaxDoubleHotelsPerSet)
            return false;

        var rentLevelValues = ownedInSet.Select(p => (int)p.RentLevel).ToList();
        //Cant build on this property since other properties in the set are lower than this property's rent level
        if(rentLevelValues.Any(l => l < (int)property.RentLevel))
            return false;
        
        //Passed all the checks, this property's rent level can be increased
        return true;
    }
    
    public bool CanIncreaseRentLevelForAllInSet(PropertySet set)
        => CanIncreaseRentLevelForAllInSet(Metadata.CurrentPlayerId, set);

    public bool CanIncreaseRentLevelForAllInSet(string playerId, PropertySet set)
    {
        var indexes = PropertySetHelper.GetIndexes(set);
        
        var properties = GetOwnedProperties(playerId, set, includeMortgaged: false, includeReserved: false);
        //Number of properties where we are building a house on (set, 1 house, 2 houses, or 3 houses)
        var numHouses = properties.Count(p => p.RentLevel is >= RentLevel.SET and < RentLevel.FOUR_HOUSES);
        //Number of properties where we are building a new hotel on (4 houses)
        var numNewHotels = properties.Count(p => p.RentLevel is RentLevel.FOUR_HOUSES);
        var extraHouses = numNewHotels * 4; //New hotel frees up 4 houses each
        
        var numHotels = properties.Count(p => p.RentLevel is RentLevel.HOTEL);
        numHotels += numNewHotels;
        
        //Check there is enough houses and hotels left to build on all properties in the set
        var (housesLeft, hotelsLeft) = GetHousesAndHotelsLeft();
        if (numHouses > (housesLeft + extraHouses) || numHotels > hotelsLeft)
            return false;

        var result = true;
        foreach (var i in indexes)
        {
            var canIncrease = CanIncreaseRentLevel(playerId, i);
            if(!canIncrease) result = false;
        }
        
        if(!result)
            return false;
        
        //Final double hotel check (false when building double hotels)
        //You can ONLY build ONE double hotel PER set, and ONLY when you have all 3 hotels
        //Therefore, any hotels on a property in the set will prevent a "build on all" in the set,
        //since hotel is the max (for bulk build)
        var ownedProperties = GetOwnedProperties(playerId, set);
        return ownedProperties.All(p => p.RentLevel != RentLevel.HOTEL);
    }
    
    
    public bool CanIncreaseRentLevelForAll()
        => CanIncreaseRentLevelForAll(Metadata.CurrentPlayerId);

    public bool CanIncreaseRentLevelForAll(string playerId)
    {
        //Every complete buildable set the player holds must itself be increaseable
        //(mortgaged/reserved excluded by the filter; stations/utilities by
        //GetOwnedSets). No complete sets → vacuously true.
        var owned = GetOwnedProperties(playerId, includeMortgaged: false, includeReserved: false);
        return PropertySetHelper.GetOwnedSets(playerId, owned)
            .All(set => CanIncreaseRentLevelForAllInSet(playerId, set));
    }
    
    
    
    public bool CanDecreaseRentLevel(ushort boardIndex)
        => CanDecreaseRentLevel(Metadata.CurrentPlayerId, boardIndex);

    public bool CanDecreaseRentLevel(string playerId, ushort boardIndex)
    {
        var (success, ownedInSet, property) = RentLevelBuySellCheck(playerId, boardIndex);
        if(!success || property == null) return false;
        
        var (housesLeft, _) = GetHousesAndHotelsLeft();
        if(property.RentLevel == RentLevel.HOTEL && housesLeft < 4)
            //Selling a hotel requires 4 houses to be left (hotel -> 4 houses)
            return false;
        
        //Cannot sell houses on a purged property (purged flag removed when rent level stabilises)
        if(property.IsPurged)
            return false;
        
        //Cant sell more than SET level (no houses left to sell) on a property
        if(property.RentLevel == RentLevel.SET)
            return false;
        
        //Exclude purged properties from rent level values
        //this allows other properties in a set to sell houses, while purged property remains well below rent level in set
        var rentLevelValues = ownedInSet.Where(p => !p.IsPurged)
            .Select(p => (int)p.RentLevel).ToList();
        
        //Cant sell on this property since other properties in the set are higher than this property's rent level
        if(rentLevelValues.Any(l => l > (int)property.RentLevel))
            return false;
        
        //Passed all the checks, this property's rent level can be decreased
        return true;
    }
    
    
    public bool CanDecreaseRentLevelForAllInSet(PropertySet set)
        => CanDecreaseRentLevelForAllInSet(Metadata.CurrentPlayerId, set);

    public bool CanDecreaseRentLevelForAllInSet(string playerId, PropertySet set)
    {
        var indexes = PropertySetHelper.GetIndexes(set);
        
        var properties = GetOwnedProperties(playerId, set, includeMortgaged: false, includeReserved: false);
        
        var numNew4Houses = properties.Count(p => p.RentLevel is RentLevel.HOTEL);      //H -> 4h
        var housesRequired = numNew4Houses * 4; //Need 4 houses per property downgrading to 4houses
        
        //Number of properties downgrading where a house is sold (returned back)
        var numHouses = properties.Count(p => p.RentLevel is > RentLevel.SET and <= RentLevel.FOUR_HOUSES);
        
        //Check there is enough houses to sell (no hotel check is requird)
        var (housesLeft, _) = GetHousesAndHotelsLeft();
        if (housesRequired > (housesLeft + numHouses))
            return false;
        
        var result = true;
        foreach (var i in indexes)
        {
            var canDecrease = CanDecreaseRentLevel(playerId, i);
            if (!canDecrease) result = false;
        }

        return result;
    }
    
    
    public bool CanDecreaseRentLevelForAll()
        => CanDecreaseRentLevelForAll(Metadata.CurrentPlayerId);

    public bool CanDecreaseRentLevelForAll(string playerId)
    {
        //Every complete buildable set the player holds must itself be sellable-down
        //(mortgaged/reserved excluded by the filter; stations/utilities by
        //GetOwnedSets). No complete sets → vacuously true.
        var owned = GetOwnedProperties(playerId, includeMortgaged: false, includeReserved: false);
        return PropertySetHelper.GetOwnedSets(playerId, owned)
            .All(set => CanDecreaseRentLevelForAllInSet(playerId, set));
    }


    public bool CanMortgageProperty(ushort boardIndex)
        => CanMortgageProperty(Metadata.CurrentPlayerId, boardIndex);

    public bool CanMortgageProperty(string playerId, ushort boardIndex)
    {
        var resolvedSet = PropertySetHelper.ResolveSet(boardIndex);
        if (resolvedSet is null)
            return false;
        
        var set = (PropertySet)resolvedSet;
        var ownedInSet = GetOwnedProperties(playerId, set);
        
        var property = ownedInSet.FirstOrDefault(p => p.BoardIndex == boardIndex);
        if(property is null)
            return false;
        
        //Cant mortgage a property that is already mortgaged (or reserved)
        if(property.State != PropertyState.Owned)
            return false;
        
        //Cannot mortgage a property that has any houses/hotels in the set
        return !ownedInSet.Any(p => p.RentLevel is > RentLevel.SET and <= RentLevel.DOUBLE_HOTEL); 
    }
    
    #endregion
}