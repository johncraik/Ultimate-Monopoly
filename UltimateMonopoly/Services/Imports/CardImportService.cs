using System.Text.Json;
using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using MP.GameEngine.Helpers.Cards;
using MP.GameEngine.Models.Snapshot.Cards;
using UltimateMonopoly.Models.DataModels;

namespace UltimateMonopoly.Services.Imports;

public class CardImportService
{
    private readonly FilePathProvider _filePathProvider;
    private readonly IRepositoryManager _repos;

    private const int _cardTypeCount = 12;
    private readonly string[] _cardFileNames;
    

    public CardImportService(FilePathProvider filePathProvider, 
        IConfiguration config,
        IRepositoryManager repos)
    {
        _filePathProvider = filePathProvider;
        _repos = repos;
        
        //Import all card file names (fixed list, wont change in size)
        _cardFileNames = new string[_cardTypeCount];
        _cardFileNames[0] = config["Imports:Cards:Chance"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[1] = config["Imports:Cards:ComChest"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[2] = config["Imports:Cards:PercentChance"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[3] = config["Imports:Cards:PercentComChest"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[4] = config["Imports:Cards:Third"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[5] = config["Imports:Cards:Double"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[6] = config["Imports:Cards:Triple"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[7] = config["Imports:Cards:Tax"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[8] = config["Imports:Cards:Go"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[9] = config["Imports:Cards:JustVisiting"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[10] = config["Imports:Cards:FreeParking"] ?? throw new ArgumentNullException(nameof(config));
        _cardFileNames[11] = config["Imports:Cards:GoToJail"] ?? throw new ArgumentNullException(nameof(config));
    }

    public async Task<List<CardModel>> ImportCards()
    {
        //TODO - Cards will be imported via import objects:
        //With the import we then check + grab persisted IDs
        //For compile, import object is "dynamic"

        var cardImports = new List<dynamic>();
        foreach (var fileName in _cardFileNames)
        {
            var path = _filePathProvider.GetFilePath(FilePathProvider.FileCategory.Card);
            path = Path.Combine(path, fileName);
            if(!File.Exists(path))
                continue;

            var txt = await _filePathProvider.ReadFileAsync(path);
            cardImports.Add(JsonSerializer.Deserialize<dynamic>(txt));
        }
        
        //TODO: Import Objects would then be translated into a List of CardModel
        var cardList = new List<CardModel>();

        //Enrich card model list with persisted card ids
        var persistedIdsToAdd = new List<PersistedCardIds>();
        foreach (var cardModel in cardList)
        {
            var persistedIds = await GetAndPersistCardIdObj(cardModel);
            if(persistedIds != null)
                persistedIdsToAdd.Add(persistedIds);
        }
        
        if(persistedIdsToAdd.Count == 0)
            return cardList;
        
        //Add persisted IDs:
        await _repos.GetRepository<PersistedCardIds>()
            .AddAsync(persistedIdsToAdd, IUserInfo.SYSTEM_USER_ID);
        return cardList;
    }

    private async Task<PersistedCardIds?> GetAndPersistCardIdObj(CardModel card)
    {
        //Get the persisted card IDs:
        var add = false;
        var persistedIds = await _repos.GetRepository<PersistedCardIds>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive)
            .FirstOrDefaultAsync(p => p.CardText.ToLower() == card.CardText.ToLower());
        if (persistedIds == null)
        {
            var groupIdInput = card.Groups
                .Select(g => new CardGroupIdInput((ushort)g.Actions.Count));
            var conditionsCount = card.Conditions.Count;
            persistedIds = new PersistedCardIds(card.CardText, groupIdInput, (ushort)conditionsCount);
            add = true;
        }

        //Deserialise the group IDs and their action IDs:
        var persistedGroups = JsonSerializer.Deserialize<List<CardGroupIdJson>>(persistedIds.GroupIdJson)
            ?? throw new Exception("Could not deserialize persisted groups");
        
        //Set card ID
        card.CardId = persistedIds.CardId;
        var index = 0;
        if(card.Groups.Count != persistedGroups.Count)
            throw new Exception("Group count mismatch");
        
        foreach (var g in card.Groups)
        {
            //Foreach group, set group ID, group key and action IDs
            var idObj = persistedGroups[index];
            if (idObj == null)
                throw new Exception("Group id object is null");
            
            //Set group ID and group key
            g.GroupId = idObj.GroupId;
            g.GroupKey = $"{CardDisplayHelper.GroupIdentifier}{index}"; 
            
            var actionIndex = 0;
            if(g.Actions.Count != idObj.ActionIds.Length)
                throw new Exception("Action count mismatch");
            
            //Set action IDs
            foreach (var a in g.Actions)
            {
                a.ActionId = idObj.ActionIds[actionIndex];
                actionIndex++;
            }
            
            index++;
        }
        
        //Deserialise the condition IDs:
        var persistedConditions = JsonSerializer.Deserialize<List<string>>(persistedIds.ConditionIdJson)
            ?? throw new Exception("Could not deserialize persisted conditions");

        index = 0;
        if(card.Conditions.Count != persistedConditions.Count)
            throw new Exception("Condition count mismatch");
        
        foreach (var c in card.Conditions)
        {
            //Foreach condition, set condition ID
            var pc = persistedConditions[index];
            c.ConditionId = pc;
            index++;
        }
        
        return add ? persistedIds : null;
    }
}