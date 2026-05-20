using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using UltimateMonopoly.Enums.Players;
using UltimateMonopoly.Helpers;
using UltimateMonopoly.Helpers.RuleSet;

namespace UltimateMonopoly.Models.DataModels.Games;

[PrimaryKey(nameof(GameId), nameof(UserId))]
public class GamePlayer
{
    [Required]
    [MaxLength(38)]
    public string GameId { get; set; }
    [ForeignKey(nameof(GameId))]
    public Game Game { get; set; }
    
    [Required]
    [MaxLength(38)]
    public string UserId { get; set; }
    
    //Max of 8 players
    [Range(0, 7)]
    public ushort OrderId { get; private set; }
    
    [Range(0, 6)]
    public ushort Dice1 { get; private set; }
    [Range(0, 6)]
    public ushort Dice2 { get; private set; }
    
    
    //See Game Engine Doc - Moved to separate model, persisted in JSON in game snapshot:
    
    // [Range(0, 100)]
    // public ushort BoardIndex { get; private set; }
    // public PlayerDirection Direction { get; private set; }
    //
    // public uint Money { get; private set; } = RuleDictionary.StartingMoney;
    //
    //
    // [NotMapped]
    // public bool DiceNumberSet => Dice1 != 0 && Dice2 != 0;
    //
    // [NotMapped]
    // public (ushort Dice1, ushort Dice2) DiceNumber => (Dice1, Dice2);
    //
    // [Range(RuleDictionary.DefaultJailCost, uint.MaxValue)]
    // public uint JailCost { get; private set; }
    //
    // [Range(RuleDictionary.DefaultTripleBonus, uint.MaxValue)]
    // public uint TripleBonus { get; private set; }
    

    public GamePlayer()
    {
    }

    public GamePlayer(string gameId, string userId)
    {
        GameId = gameId;
        UserId = userId;
        
        //See Game Engine Doc - Moved to separate model, persisted in JSON in game snapshot:
        
        // BoardIndex = 0;
        // Direction = PlayerDirection.Forward;
        //
        // Money = RuleDictionary.StartingMoney;
        // JailCost = RuleDictionary.DefaultJailCost;
        // TripleBonus = RuleDictionary.DefaultTripleBonus;
    }

    public bool SetOrderId(ushort orderId)
    {
        if(orderId > 7)
            return false;
        
        OrderId = orderId;
        return true;
    }
    
    public bool SetDiceNumber(ushort dice1, ushort dice2)
    {
        if (dice1 > 6 || dice2 > 6)
            return false;
        
        Dice1 = dice1;
        Dice2 = dice2;
        return true;
    }
    
    

    //See Game Engine Doc - Moved to separate model, persisted in JSON in game snapshot:
    
    // public (ushort Index, ushort GoPasses) Move(ushort spaces)
    // {
    //     (BoardIndex, var goPasses) = IndexHelper.MoveIndex(BoardIndex, spaces, Direction);
    //     return (BoardIndex, goPasses);
    // }
    //
    // public (ushort Index, bool PassGo) Advance(ushort desiredIndex)
    // {
    //     (BoardIndex, var passGo) = IndexHelper.AdvanceIndex(BoardIndex, desiredIndex, Direction);
    //     return (BoardIndex, passGo);
    // }
    //
    // public void ChangeDirection()
    // {
    //     Direction = Direction switch
    //     {
    //         PlayerDirection.Forward => PlayerDirection.Backward,
    //         PlayerDirection.Backward => PlayerDirection.Forward,
    //         _ => throw new ArgumentOutOfRangeException()
    //     };
    // }
    //
    // public void AddMoney(uint amount) => Money += amount;
    //
    // public bool TakeMoney(uint amount)
    // {
    //     if(Money < amount)
    //         return false;
    //     
    //     Money -= amount;
    //     return true;
    // }
    //
    //
    // public void LeaveJail()
    // {
    //     JailCost += (uint)(JailCost * RuleDictionary.JailCostMultiplier);
    // }
    //
    // public void ResetJailCost() => JailCost = RuleDictionary.DefaultJailCost;
    //
    // public void ClaimTripleBonus() => TripleBonus += RuleDictionary.TripleBonusIncrease;
    //
    // public void ResetTripleBonus() => TripleBonus = RuleDictionary.DefaultTripleBonus;
}