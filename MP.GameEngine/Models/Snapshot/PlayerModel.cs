using System.Text.Json.Serialization;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
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
    
    public bool IsBankrupt { get; set; }

    /// <summary>
    /// Cards owned by the player (keep until needed/played upon condition)
    /// </summary>
    public List<CardModel> Cards { get; set; } = [];

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
        
        DoublesInRow = model.DoublesInRow;
        TriplesInRow = model.TriplesInRow;
        
        TripleBonus = model.TripleBonus;
        JailCost = model.JailCost;
        
        TurnsToMiss = model.TurnsToMiss;
        ExtraTurns = model.ExtraTurns;
        JailTurnCounter = model.JailTurnCounter;
        MaxJailTurnsOverride = model.MaxJailTurnsOverride;
        
        IsBankrupt = model.IsBankrupt;
        
        Cards = model.Cards.Select(c => new CardModel(c)).ToList();
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

}