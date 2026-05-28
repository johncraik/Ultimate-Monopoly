using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
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
        
        var afterPov = players.Where(p => p.OrderId > povPlayer.OrderId);
        var beforePov = players.Where(p => p.OrderId < povPlayer.OrderId);
        
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
        
        return properties.Count(p => (p.StreetRuleQualifier == StreetRuleQualifier.NeverBuiltOn 
                                      || p.StreetRuleQualifier == StreetRuleQualifier.Qualified) 
                                     && streetIndexes.Contains(p.BoardIndex)) == streetIndexes.Count;
    }

    #endregion
    
    
    
}