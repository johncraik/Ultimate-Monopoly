using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

public class IncomeStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        uint rentEarned = 0;
        
        uint timesPassedGo = 0;
        uint moneyCollectedFromGo = 0;
        
        uint moneyFromSelling = 0;
        uint moneyFromCards = 0;
        uint moneyFromMortgaging = 0;
        uint moneyFromFreeParking = 0;
        
        uint moneyFromTriples = 0;
        uint moneyFromSnakeEyes = 0;
        uint moneyFromDiceNumber = 0;
        
        uint moneyFromDeals = 0;
        uint moneyFromBankruptcies = 0;
        
        foreach (var turn in snapshot.Turns)
        {
            //Filter financial events for this player in this turn
            var financialEvents = turn.Events
                .OfType<FinancialTransactionReceipt>()
                .Where(fe => fe.PlayerId == player.PlayerId && fe.Amount > 0)
                .ToList();

            //Determine if the player landed on GO this turn (used in switch)
            var landedOnGo = turn.Events
                .OfType<PlayerMovedReceipt>()
                .Any(me => me.PlayerId == player.PlayerId && me.FinalBoardIndex == 0);
                
            
            foreach (var fe in financialEvents)
            {
                //Switch the amount to be positive, and then classify by reason
                var amount = (uint)fe.Amount;
                switch (fe.Reason)
                {
                    case FinancialReason.Rent:
                        rentEarned += amount;
                        break;
                    case FinancialReason.GoBonus:
                        moneyCollectedFromGo += amount;
                        if(!landedOnGo)
                            timesPassedGo++;
                        break;
                    case FinancialReason.DiceNumBonus:
                        moneyFromDiceNumber += amount;
                        break;
                    case FinancialReason.SneakEyes:
                        moneyFromSnakeEyes += amount;
                        break;
                    case FinancialReason.TripleBonus:
                        moneyFromTriples += amount;
                        break;
                    case FinancialReason.FreeParkingTake:
                        moneyFromFreeParking += amount;
                        break;
                    case FinancialReason.Sell:
                        moneyFromSelling += amount;
                        break;
                    case FinancialReason.Mortgage:
                        moneyFromMortgaging += amount;
                        break;
                    case FinancialReason.CardPayout:
                        moneyFromCards += amount;
                        break;
                    case FinancialReason.Deal:
                        moneyFromDeals += amount;
                        break;
                    case FinancialReason.BankruptedPlayer:
                        moneyFromBankruptcies += amount;
                        break;
                }
            }
        }
        
        //Set the stats:
        record.RentEarned = rentEarned;

        record.TimesPassedGo = timesPassedGo;
        record.MoneyCollectedFromGo = moneyCollectedFromGo;
        
        record.MoneyFromSelling = moneyFromSelling;
        record.MoneyFromCards = moneyFromCards;
        record.MoneyFromMortgaging = moneyFromMortgaging;
        record.MoneyFromFreeParking = moneyFromFreeParking;
        
        record.MoneyFromTriples = moneyFromTriples;
        record.MoneyFromSnakeEyes = moneyFromSnakeEyes;
        record.MoneyFromDiceNumber = moneyFromDiceNumber;
        
        record.MoneyFromDeals = moneyFromDeals;
        record.MoneyFromBankruptPlayers = moneyFromBankruptcies;
        
        return record;
    }
}