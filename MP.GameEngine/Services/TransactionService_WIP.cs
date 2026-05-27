using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services;

public class TransactionService_WIP
{
    public TransactionService_WIP()
    {
    }

    public async Task PayRent(Framework.GameEngine engine, PlayerModel player, ushort propertyIndex, CancellationToken ct)
    {
        var space = engine.Cache.Board.GetBoardSpace(propertyIndex);
        if(!space.IsRentable)
            throw new InvalidOperationException("Property is not rentable");
        
        var property = engine.Cache.Game.GetPropertySpace(space.Index);
        if (property == null) throw new InvalidOperationException("Property not found in game property list.");
        
        if(property.OwnerPlayerId == null) 
            throw new InvalidOperationException("Property is not owned");
        
        var propOwner = engine.Cache.Game.GetPlayer(property.OwnerPlayerId);
        if (propOwner == null) throw new InvalidOperationException("Property owner not found in game players list.");

        var rent = space.GetRent(property.RentLevel);
        if (rent == null) throw new InvalidOperationException("Rent not found for property");
        
        var amount = (uint)rent.Value;
        var result = await ProcessTransaction(engine, player, -amount, TransactionCounterparty.Player, propOwner, false, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        //Emit event for player paying the rent:
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = -amount,
            Counterparty = TransactionCounterparty.Player,
            CounterpartyPlayerId = property.OwnerPlayerId,
            Reason = FinancialReason.Rent,
            CounterpartyPropertyIndex = property.BoardIndex
        });
        
        //Emit event for property owner receiving rent:
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = property.OwnerPlayerId ?? throw new InvalidOperationException("Property is not owned"),
            Amount = amount,
            Counterparty = TransactionCounterparty.Player,
            CounterpartyPlayerId = player.PlayerId,
            Reason = FinancialReason.Rent,
            CounterpartyPropertyIndex = property.BoardIndex
        });
    }

    public async Task PayTax(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
    { 
        var result = await ProcessTransaction(engine, player, -amount, TransactionCounterparty.FreeParking, null, false, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        //Emit event for player paying the tax:
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = -amount,
            Counterparty = TransactionCounterparty.FreeParking,
            Reason = FinancialReason.Tax
        });
    }

    public async Task ReceiveGoBonus(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
    {
        var result = await ProcessTransaction(engine, player, amount, TransactionCounterparty.Bank, null, false, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = amount,
            Counterparty = TransactionCounterparty.Bank,
            Reason = FinancialReason.GoBonus
        });
    }

    public async Task PayJailFee(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var amount = player.JailCost;
        var result = await ProcessTransaction(engine, player, -amount, TransactionCounterparty.FreeParking, null, true, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = amount,
            Counterparty = TransactionCounterparty.FreeParking,
            Reason = FinancialReason.JailFee
        });
    }

    public async Task PayIntoFreeParking(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
    {
        var result = await ProcessTransaction(engine, player, -amount, TransactionCounterparty.FreeParking, null, false, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = -amount,
            Counterparty = TransactionCounterparty.FreeParking,
            Reason = FinancialReason.FreeParkingPay
        });
    }

    public async Task TakeFromFreeParking(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
    {
        var result = await ProcessTransaction(engine, player, amount, TransactionCounterparty.FreeParking, null, false, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = amount,
            Counterparty = TransactionCounterparty.FreeParking,
            Reason = FinancialReason.FreeParkingTake
        });   
    }

    public async Task TakeoutLoan(Framework.GameEngine engine, PlayerModel player, uint amount, CancellationToken ct)
    {
        var result = await ProcessTransaction(engine, player, -amount, TransactionCounterparty.Bank, null, false, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = -amount,
            Counterparty = TransactionCounterparty.Bank,
            Reason = FinancialReason.LoanTake
        });
    }

    public async Task RepayLoan(Framework.GameEngine engine, PlayerModel player, uint amount, bool requiredPayment, CancellationToken ct)
    {
        var result = await ProcessTransaction(engine, player, amount, TransactionCounterparty.Bank, null, !requiredPayment, ct);
        if (result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return;
        
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = amount,
            Counterparty = TransactionCounterparty.Bank,
            Reason = FinancialReason.LoanRepay
        });   
    }

    public async Task<bool> TryPurchaseProperty(Framework.GameEngine engine, PlayerModel player, ushort propertyIndex, CancellationToken ct)
    {
        var space = engine.Cache.Board.GetBoardSpace(propertyIndex);
        if (!space.IsPurchasable)
            throw new InvalidOperationException("Property is not purchasable");

        var property = engine.Cache.Game.GetPropertySpace(space.Index);
        if (property == null) throw new InvalidOperationException("Property not found in game property list.");
        
        if(property.OwnerPlayerId != null || property.State != PropertyState.NotOwned)
            throw new InvalidOperationException("Property is already owned or in free parking");
        
        var amount = (uint)(space.PurchaseCost ?? throw new InvalidOperationException("Property has no purchase cost"));
        var result = await ProcessTransaction(engine, player, -amount, TransactionCounterparty.Bank, null, true, ct);
        if(result is TransactionResult.InvalidAmount or TransactionResult.ShortfallPassed)
            //Transaction was invalid or cancelled
            return false;
        
        property.OwnProperty(player.PlayerId);
        engine.EventEmitter.Emit(new FinancialTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Amount = -amount,
            Counterparty = TransactionCounterparty.Bank,
            Reason = FinancialReason.Purchase,
            CounterpartyPropertyIndex = property.BoardIndex
        });
        
        engine.EventEmitter.Emit(new PropertyTransactionReceipt
        {
            PlayerId = player.PlayerId,
            Value = 1,
            Counterparty = TransactionCounterparty.Bank,
            SetsOnly = false
        });
        return true;
    }



    private enum TransactionResult
    {
        /// <summary>
        /// Full transaction completed successfully
        /// </summary>
        Success,
        
        /// <summary>
        /// Shortfall detected in payer or receiver, and it was ignored (transaction cancelled)
        /// </summary>
        ShortfallPassed,
        
        /// <summary>
        /// Shortfall detected in payer or receiver, and it was not resolved (receiver still needs money adding to balance)
        /// </summary>
        ShortfallUnresolved,
        
        /// <summary>
        /// Shortfall detected in payer or receiver, and it was resolved (receiver was paid for transaction)
        /// </summary>
        ShortfallResolved,
        
        /// <summary>
        /// Amount is zero
        /// </summary>
        InvalidAmount
    }

    private async Task<TransactionResult> ProcessTransaction(Framework.GameEngine engine, PlayerModel player, 
        long amount, TransactionCounterparty counterparty, PlayerModel? counterpartyPlayer, bool passOnShortfall, CancellationToken ct)
    {
        if (amount == 0) return TransactionResult.InvalidAmount;
        
        var payingPlayer = player;
        var receivingPlayer = counterpartyPlayer;
        if (amount > 0)
        {
            //NEGATIVE - 'player' receives, 'counterpartyPlayer' pays
            payingPlayer = counterpartyPlayer;
            receivingPlayer = player;
        }

        if(payingPlayer == null && receivingPlayer == null)
            throw new InvalidOperationException("Cannot process transaction without a player or counterparty");
        
        if (payingPlayer != null)
        {
            var initialBalance = payingPlayer.Money;
            var finalBalance = initialBalance + amount;

            var result = TransactionResult.Success;
            if(finalBalance < 0)
            {
                if(passOnShortfall) return TransactionResult.ShortfallPassed;
                result = await ResolveShortfall(engine, payingPlayer.PlayerId, initialBalance, amount, receivingPlayer?.PlayerId, null, ct);
            }

            if (result != TransactionResult.Success)
                return result;
            
            payingPlayer.Money = (uint)finalBalance;
        }
        else if (counterparty == TransactionCounterparty.FreeParking)
        {
            engine.Cache.Game.FreeParkingAmount -= (uint)Math.Abs(amount);
        }

        if (receivingPlayer != null)
        {
            var initialBalance = receivingPlayer.Money;
            var finalBalance = initialBalance + amount;
            
            //Should never be negative (as they are receiving money)
            receivingPlayer.Money = (uint)finalBalance;
        }
        else if (counterparty == TransactionCounterparty.FreeParking)
        {
            engine.Cache.Game.FreeParkingAmount += (uint)Math.Abs(amount);
        }
        
        engine.Cache.SaveChanges();
        return TransactionResult.Success;
    }
    
    private async Task<TransactionResult> ResolveShortfall(Framework.GameEngine engine, string playerId, uint initialBalance, long amount,
        string? counterpartyPlayerId, ushort? counterpartyPropertyIndex, CancellationToken ct)
    {
        var response = await engine.PromptProvider.RequestAsync(new ShortfallPrompt
        {
            PlayerId = playerId,
            Title = "You do not have enough money!",
            Body = "You must choose one of the options below.",
            PlayerBalance = initialBalance,
            Cost = (uint)Math.Abs(amount),
            OwedToPlayerId = counterpartyPlayerId
        }, ct);

        switch (response.Action)
        {
            case ShortfallAction.TakeLoan:
                //TODO Call loan service
                break;
            case ShortfallAction.Mortgage:
                //TODO call property service
                break;
            case ShortfallAction.SellHouses:
                //TODO call property service
                break;
            case ShortfallAction.ProposeDeal:
                //TODO call deal service
                break;
            case ShortfallAction.DeclareBankruptcy:
                //TODO call bankruptcy service
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        //TODO - sub-services in switch will return object (or bool, not done yet) on whether the receiving player has been credited
        //For example, a proposed and accepted deal may be between the paying and receiving player as a valid way to pay for rent
        //In other examples, the receiving counterparty may be the bank or free parking; and thus will not be credited in sub-services
        var returnValue = TransactionResult.ShortfallResolved;
        
        if(returnValue == TransactionResult.ShortfallResolved)
            engine.Cache.SaveChanges();
        
        return returnValue;
    }
}