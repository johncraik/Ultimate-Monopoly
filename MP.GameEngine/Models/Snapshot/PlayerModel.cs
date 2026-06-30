using System.Text.Json.Serialization;
using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Models.Snapshot;

public class PlayerModel
{
    //Player metadata linking to database models
    public string PlayerId { get; set; }
    public ushort OrderId { get; set; }
    public ushort Dice1 { get; set; }
    public ushort Dice2 { get; set; }
    
    public bool HasPassedInitialGo { get; set; }
    public bool InitialRoll { get; set; }
    public uint Money { get; set; }
    public ushort BoardIndex { get; set; }
    public PlayerDirection Direction { get; set; }
    
    //Get a third card for number being rolled
    public bool GetThirdCard { get; set; }
    
    public ushort DoublesInRow { get; set; }
    public ushort TriplesInRow { get; set; }
    
    public uint TripleBonus { get; set; }
    public uint JailCost { get; set; }
    
    public ushort TurnsToMiss { get; set; }
    [JsonIgnore]
    public bool MissNextTurn => TurnsToMiss > 0;
    
    public ushort ExtraTurns { get; set; }
    [JsonIgnore]
    public bool HasExtraTurns => ExtraTurns > 0 && !MissNextTurn; //Miss turns takes precedence
    
    
    [JsonIgnore]
    public bool IsInJail => BoardIndex == IndexHelper.JailSpace;
    public ushort JailTurnCounter { get; set; }
    public ushort? MaxJailTurnsOverride { get; set; }
    public ushort? MinJailTurns { get; set; }
    public bool CollectRentInJail { get; set; }

    /// <summary>One-shot: the player's next jail exit waives the fee ("befriend a prison guard"). Set by a
    /// <c>JailAction.ModifyLeaveFee</c> with <c>FreeNextExit</c>; consumed (charge skipped, flag cleared) in <c>PayJailFee</c>.</summary>
    public bool FreeNextJailExit { get; set; }

    [JsonIgnore]
    public bool CanLeaveJail => !IsInJail || (IsInJail && (MinJailTurns == null || JailTurnCounter >= MinJailTurns));
    
    public bool IsBankrupt { get; set; }

    /// <summary>Held free-hotel credits (the "receive a free hotel" tax card — R-06). Each credit waives the
    /// build cost of one future hotel (a <see cref="RentLevel.FOUR_HOUSES"/> → <see cref="RentLevel.HOTEL"/>
    /// step), consumed in <c>BuildingService.BuildOnProperties</c>. Granted when a hotel can't be placed
    /// immediately — the player has no four-house street, or the table hotel pool is empty.</summary>
    public ushort FreeHotels { get; set; }

    /// <summary>
    /// Cards owned by the player (keep until needed/played upon condition)
    /// </summary>
    //public List<CardModel> Cards { get; set; } = [];
    public List<PlayerCardInstance> CardInstances { get; set; } = [];
    

    /// <summary>
    /// All loans taken out by the player, including those that have been paid off
    /// </summary>
    public List<LoanModel> Loans { get; set; } = [];
    
    
    public List<PropertySet> FPHandedInSets { get; set; } = [];

    public PlayerModel()
    {
    }

    public PlayerModel(PlayerModel model)
    {
        PlayerId = model.PlayerId;
        OrderId = model.OrderId;
        Dice1 = model.Dice1;
        Dice2 = model.Dice2;
        
        InitialRoll = model.InitialRoll;
        HasPassedInitialGo = model.HasPassedInitialGo;
        Money = model.Money;
        BoardIndex = model.BoardIndex;
        Direction = model.Direction;
        
        GetThirdCard = model.GetThirdCard;
        
        DoublesInRow = model.DoublesInRow;
        TriplesInRow = model.TriplesInRow;
        
        TripleBonus = model.TripleBonus;
        JailCost = model.JailCost;
        
        TurnsToMiss = model.TurnsToMiss;
        ExtraTurns = model.ExtraTurns;
        
        JailTurnCounter = model.JailTurnCounter;
        MaxJailTurnsOverride = model.MaxJailTurnsOverride;
        MinJailTurns = model.MinJailTurns;
        CollectRentInJail = model.CollectRentInJail;
        FreeNextJailExit = model.FreeNextJailExit;
        
        IsBankrupt = model.IsBankrupt;
        FreeHotels = model.FreeHotels;

        //Cards = model.Cards.Select(c => new CardModel(c)).ToList();
        CardInstances = model.CardInstances.Select(c => new PlayerCardInstance(c)).ToList();
        Loans = model.Loans.Select(l => new LoanModel(l)).ToList();

        FPHandedInSets = [..model.FPHandedInSets];
    }


    #region Player Primitive Methods

    public void FlipDirection(Services.Framework.GameEngine engine)
    {
        if (!HasPassedInitialGo)
        {
            //Cite rule and return:
            engine.CiteRule(RuleCode.Move_DirectionLockedUntilGo);
            return;
        }
        
        var initialDirection = Direction;
        Direction = Direction == PlayerDirection.Forward
            ? PlayerDirection.Backward
            : PlayerDirection.Forward;
            
        //Cite rule and emit receipt:
        engine.CiteRule(RuleCode.Double_DirectionChange);
        engine.EventEmitter.Emit(new PlayerDirectionChangedReceipt
        {
            PlayerId = PlayerId,
            InitialDirection = initialDirection,
            FinalDirection = Direction
        });
    }


    public bool IsDiceNumber(DiceRoll roll)
    {
        if(roll.RollType == DiceRollType.Triple)
            return false;
        
        var d1 = roll.Die1;
        var d2 = roll.Die2 ?? throw new ArgumentNullException(nameof(roll.Die2), "Die2 cannot be null for dice roll validation.");
        
        return (d1 == Dice1 && d2 == Dice2) || (d1 == Dice2 && d2 == Dice1);
    }


    public bool CanTakeLoan()
        => Loans.Count(l => l.IsOutstanding) < RuleDictionary.MaxLoans;

    public List<LoanModel> GetOutstandingLoans()
        => Loans.Where(l => l.IsOutstanding).ToList();
    
    public List<LoanModel> GetPaidLoans()
        => Loans.Where(l => !l.IsOutstanding).ToList();

    public uint LoanTotalAmount()
    {
        var outstanding = GetOutstandingLoans();
        return (uint)outstanding.Sum(l => l.Amount);
    }

    public uint MinimumLoanRepayment()
    {
        var total = LoanTotalAmount();
        return (uint)Math.Round((total * RuleDictionary.LoanRepayment), MidpointRounding.AwayFromZero);
    }

    public LoanModel? FirstOutstandingLoan()
    {
        var loans = GetOutstandingLoans();
        if (loans.Count == 0)
            return null;
        
        return loans.MinBy(l => l.DateTaken) 
               ?? throw new InvalidOperationException("No outstanding loans found.");
    }

    #endregion


    #region Cards

    public async Task<List<CardModel>> GetCards(ICardCacheService cache)
    {
        var cards = await cache.GetCards();
        return CardInstances
            .Select(i =>
            {
                var card = cards.FirstOrDefault(c => c.CardId == i.CardId);
                return card == null ? null : new CardModel(card, i);
            })
            .Where(c => c != null)
            .ToList()!;
    }
    
    
    public async Task<CardModel?> GetOutOfJailCard(ICardCacheService cache)
        => (await cache.GetCards())
            .FirstOrDefault(c => CardInstances
                                     .Select(i => i.CardId)
                                     .Contains(c.CardId) &&
                c.Groups.Any(g => 
                    g.Actions.Any(a =>
                    {
                        if(a is not JailAction j) return false;
                        return j.Kind == JailKind.Release;
                    })));

    public async Task<List<CardModel>> GetOutOfJailCards(ICardCacheService cache)
        => (await cache.GetCards())
            .Where(c => CardInstances
                            .Select(i => i.CardId)
                            .Contains(c.CardId) &&
                c.Groups.Any(g => 
                    g.Actions.Any(a =>
                    {
                        if(a is not JailAction j) return false;
                        return j.Kind == JailKind.Release;
                    })))
            .ToList();
    
    public async Task<List<CardModel>> GetPlayableCards(ICardCacheService cache)
        => (await cache.GetCards())
            .Where(c => CardInstances
                            .Select(i => i.CardId)
                            .Contains(c.CardId) 
                        && c.ConditionType != CardConditionType.None 
                        && c.Conditions.Any(cd => cd.Trigger is CardTrigger.OnTurnStart or CardTrigger.OnSpaceLand 
                                                  || cd.Trigger.HasFlag(CardTrigger.OnTurnStart) 
                                                  || cd.Trigger.HasFlag(CardTrigger.OnSpaceLand)))
            .ToList();
    
    public async Task<List<CardModel>> GetOtherCards(ICardCacheService cache)
        => (await cache.GetCards())
            .Where(c => CardInstances
                            .Select(i => i.CardId)
                            .Contains(c.CardId) 
                        && c.ConditionType != CardConditionType.None 
                        && c.Conditions.All(cd => cd.Trigger is not CardTrigger.OnTurnStart and not CardTrigger.OnSpaceLand 
                                                  && !cd.Trigger.HasFlag(CardTrigger.OnTurnStart) 
                                                  && !cd.Trigger.HasFlag(CardTrigger.OnSpaceLand)))
            .ToList();

    #endregion

}