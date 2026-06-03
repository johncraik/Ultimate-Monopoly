using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Helpers.RuleSet;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Imports;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services;
using MP.GameEngine.Services.Framework;

// ReSharper disable InconsistentNaming
namespace MP.GameEngine.Tests.ServiceTests;

/// <summary>
/// Covers every public transaction type on <see cref="TransactionService"/> —
/// the sign convention, counterparty mirroring, Free Parking pot algebra,
/// receipt emission, rounding (via <c>MoneyHelper</c>), the shortfall flow, and
/// the no-op rules. Rounding is <see cref="GameRoundingRule.None"/> unless a test
/// is specifically about rounding, so the arithmetic in assertions is the raw
/// amount.
///
/// The "Dice number bonus" region at the end reproduces the multi-payer bug
/// reported from manual play: the roller collected only the bank bonus instead
/// of the bank bonus plus £100 from every other player. Those tests assert the
/// correct (spec) behaviour — <c>game-rules.md</c> Dice Rolls rule 3 — and are
/// expected to FAIL until the stale-counterparty-reference issue in
/// <see cref="TransactionService"/> / <c>PlayerService.ResolveDiceNumber</c> is
/// fixed. See the notes on each test.
/// </summary>
public class TransactionService_Tests
{
    // ─── Identities ─────────────────────────────────────────────────────

    private const string P1 = "player-1";
    private const string P2 = "player-2";
    private const string P3 = "player-3";
    private const string Host = "host";

    private const ushort PropertyIndex = 1;   // Old Kent Road (Brown)
    private const ushort TaxIndex = 4;         // Income Tax

    // ─── Stubs ──────────────────────────────────────────────────────────

    /// <summary>Snapshots are never written by TransactionService — no-op stub for the engine bundle.</summary>
    private sealed class NoOpSnapshotService : ISnapshotService
    {
        public Task CreateSnapshotAsync(GameModel game, bool completeTransaction = true) => Task.CompletedTask;
    }

    /// <summary>No-op notifier — these tests don't assert on broadcasts.</summary>
    private sealed class NoOpNotifier : IEngineNotifier
    {
        public void PromptOpened(string gameId, Prompt prompt, string concurrencyStamp) { }
        public void PromptClosed(string gameId, string promptId, string concurrencyStamp) { }
        public void StateChanged(GameCacheModel cache) { }
    }

    // ─── Fixtures ───────────────────────────────────────────────────────

    private static PlayerModel Player(string id, ushort orderId = 0, uint money = 1500,
        uint jailCost = 0, ushort boardIndex = 0)
        => new()
        {
            PlayerId = id,
            OrderId = orderId,
            Money = money,
            JailCost = jailCost,
            BoardIndex = boardIndex
        };

    private static PropertyModel Prop(ushort index, string? owner, PropertyState state,
        RentLevel rentLevel = RentLevel.SINGLE)
        => new()
        {
            Name = $"prop-{index}",
            BoardIndex = index,
            OwnerPlayerId = owner,
            State = state,
            RentLevel = rentLevel
        };

    /// <summary>
    /// Board with one ownable property (index 1, rents starting £2) and one tax
    /// space (index 4). Enough for the rent transaction tests.
    /// </summary>
    private static Board RentBoard()
    {
        var property = new BoardSpace(new BoardSpaceJsonImport
        {
            Name = "Old Kent Road",
            Index = PropertyIndex,
            SpaceType = nameof(BoardSpaceType.Property),
            PurchaseCost = 60,
            BuildCost = 50
        });
        property.SetRents([2, 4, 10, 30, 90, 160, 250, 500]);

        var tax = new BoardSpace(new BoardSpaceJsonImport
        {
            Name = "Income Tax",
            Index = TaxIndex,
            SpaceType = nameof(BoardSpaceType.Tax),
            Tax = 200
        });

        return new Board("Rent Board", [property, tax]);
    }

    private static GameCacheModel CreateCache(
        GameRoundingRule rounding = GameRoundingRule.None,
        uint freeParking = 0,
        List<PlayerModel>? players = null,
        List<PropertyModel>? properties = null,
        Board? board = null,
        string currentPlayerId = P1)
    {
        var dto = new GameDTO(
            id: "game-1",
            name: "Test Game",
            boardId: "board-1",
            roundingRule: rounding,
            hostPlayerId: Host,
            state: GameState.InPlay,
            outcome: GameOutcome.None);

        var game = new GameModel
        {
            GameId = "game-1",
            Metadata = new TurnMetadata
            {
                CurrentTurnId = "turn-1",
                CurrentPlayerId = currentPlayerId,
                TurnNumber = 1
            },
            Players = players ?? [Player(P1)],
            Properties = properties ?? [],
            FreeParkingAmount = freeParking
        };

        return new GameCacheModel(dto, game, board ?? new Board("Empty", []));
    }

    private static Services.Framework.GameEngine CreateEngine(GameCacheModel cache)
        => new(cache, new NoOpSnapshotService(), new NoOpNotifier());

    private static TransactionService CreateService() => new();

    private static uint Money(GameCacheModel cache, string playerId)
        => cache.Game.GetPlayer(playerId)!.Money;

    private static List<FinancialTransactionReceipt> Receipts(GameCacheModel cache)
        => cache.Events.OfType<FinancialTransactionReceipt>().ToList();

    private static FinancialTransactionReceipt SingleReceipt(GameCacheModel cache)
        => Assert.Single(Receipts(cache));

    private static async Task<Prompt> WaitForPendingPromptAsync(GameCacheModel cache, int timeoutMs = 1000)
    {
        var waited = 0;
        while (cache.PendingPrompt is null)
        {
            if (waited >= timeoutMs) throw new TimeoutException("Expected a prompt to open but none did.");
            await Task.Delay(5);
            waited += 5;
        }
        return cache.PendingPrompt.Prompt;
    }


    // ════════════════════════════════════════════════════════════════════
    //  Debits — to Bank / Free Parking (single receipt, no counterparty player)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PayTax_DebitsPlayer_AndIncreasesFreeParkingPot()
    {
        var cache = CreateCache(freeParking: 30, players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);

        Assert.Equal(300u, Money(cache, P1));
        Assert.Equal(230u, cache.Game.FreeParkingAmount);
    }

    [Fact]
    public async Task PayTax_EmitsSingleReceipt_FromPayerPerspective()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);

        var receipt = SingleReceipt(cache);
        Assert.Equal(P1, receipt.PlayerId);
        Assert.Equal(-200, receipt.Amount);
        Assert.Equal(FinancialReason.Tax, receipt.Reason);
        Assert.Equal(TransactionCounterparty.FreeParking, receipt.Counterparty);
        Assert.Null(receipt.CounterpartyPlayerId);
    }

    [Fact]
    public async Task PayJailFee_UsesPlayerJailCost_PaysIntoFreeParking()
    {
        var cache = CreateCache(players: [Player(P1, money: 500, jailCost: 75)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayJailFee(engine, cache.Game.GetPlayer(P1)!, CancellationToken.None);

        Assert.Equal(425u, Money(cache, P1));
        Assert.Equal(75u, cache.Game.FreeParkingAmount);
        Assert.Equal(FinancialReason.JailFee, SingleReceipt(cache).Reason);
    }

    [Fact]
    public async Task PayIntoFreeParking_IncreasesPot_ReasonFreeParkingPay()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayIntoFreeParking(engine, cache.Game.GetPlayer(P1)!, 120, CancellationToken.None);

        Assert.Equal(380u, Money(cache, P1));
        Assert.Equal(120u, cache.Game.FreeParkingAmount);
        Assert.Equal(FinancialReason.FreeParkingPay, SingleReceipt(cache).Reason);
    }

    [Fact]
    public async Task RepayLoan_DebitsPlayer_BankCounterparty_FreeParkingUnchanged()
    {
        var cache = CreateCache(freeParking: 99, players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.RepayLoan(engine, cache.Game.GetPlayer(P1)!, 150, CancellationToken.None);

        Assert.Equal(350u, Money(cache, P1));
        Assert.Equal(99u, cache.Game.FreeParkingAmount);          // bank transaction never touches the pot
        var receipt = SingleReceipt(cache);
        Assert.Equal(FinancialReason.LoanRepay, receipt.Reason);
        Assert.Equal(TransactionCounterparty.Bank, receipt.Counterparty);
    }

    [Fact]
    public async Task PayMortgageFee_DebitsPlayer_SetsPropertyIndexOnReceipt()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayMortgageFee(engine, cache.Game.GetPlayer(P1)!, 6, PropertyIndex, CancellationToken.None);

        Assert.Equal(494u, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(FinancialReason.MortgageFee, receipt.Reason);
        Assert.Equal(PropertyIndex, receipt.CounterpartyPropertyIndex);
    }

    [Fact]
    public async Task PurchaseProperty_Affordable_DebitsPlayer_ReceiptHasPropertyIndex()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PurchaseProperty(engine, cache.Game.GetPlayer(P1)!, 60, PropertyIndex, CancellationToken.None);

        Assert.Equal(440u, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(FinancialReason.Purchase, receipt.Reason);
        Assert.Equal(PropertyIndex, receipt.CounterpartyPropertyIndex);
    }

    [Fact]
    public async Task WinAuction_DebitsPlayer_ReasonAuction()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.WinAuction(engine, cache.Game.GetPlayer(P1)!, 110, PropertyIndex, CancellationToken.None);

        Assert.Equal(390u, Money(cache, P1));
        Assert.Equal(FinancialReason.Auction, SingleReceipt(cache).Reason);
    }

    [Fact]
    public async Task PayForBuild_DebitsPlayer_ReasonBuild()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayForBuild(engine, cache.Game.GetPlayer(P1)!, 50, PropertyIndex, CancellationToken.None);

        Assert.Equal(450u, Money(cache, P1));
        Assert.Equal(FinancialReason.Build, SingleReceipt(cache).Reason);
    }

    [Fact]
    public async Task PayToUnmortgage_DebitsPlayer_ReasonUnmortgage()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayToUnmortgage(engine, cache.Game.GetPlayer(P1)!, 33, PropertyIndex, CancellationToken.None);

        Assert.Equal(467u, Money(cache, P1));
        Assert.Equal(FinancialReason.Unmortgage, SingleReceipt(cache).Reason);
    }


    // ════════════════════════════════════════════════════════════════════
    //  Credits — from Bank / Free Parking (single receipt)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ReceiveGoBonus_CreditsPlayer_BankCounterparty()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ReceiveGoBonus(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);

        Assert.Equal(700u, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(200, receipt.Amount);
        Assert.Equal(FinancialReason.GoBonus, receipt.Reason);
        Assert.Equal(TransactionCounterparty.Bank, receipt.Counterparty);
    }

    [Fact]
    public async Task ReceiveDiceBonus_CreditsPlayerByDiceBonusConstant()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ReceiveDiceBonus(engine, cache.Game.GetPlayer(P1)!, CancellationToken.None);

        Assert.Equal(500u + RuleDictionary.DiceNumRolledBonus, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(RuleDictionary.DiceNumRolledBonus, receipt.Amount);
        Assert.Equal(FinancialReason.DiceNumBonus, receipt.Reason);
    }

    [Fact]
    public async Task TakeFromFreeParking_CreditsPlayer_DecreasesPot()
    {
        var cache = CreateCache(freeParking: 500, players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.TakeFromFreeParking(engine, cache.Game.GetPlayer(P1)!, 300, CancellationToken.None);

        Assert.Equal(800u, Money(cache, P1));
        Assert.Equal(200u, cache.Game.FreeParkingAmount);
        Assert.Equal(FinancialReason.FreeParkingTake, SingleReceipt(cache).Reason);
    }

    [Fact]
    public async Task TakeLoan_CreditsPlayer_ReasonLoanTake()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.TakeLoan(engine, cache.Game.GetPlayer(P1)!, 250, CancellationToken.None);

        Assert.Equal(750u, Money(cache, P1));
        Assert.Equal(FinancialReason.LoanTake, SingleReceipt(cache).Reason);
    }

    [Fact]
    public async Task ReceiveForSell_CreditsPlayer_PropertyIndexSet()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ReceiveForSell(engine, cache.Game.GetPlayer(P1)!, 25, PropertyIndex, CancellationToken.None);

        Assert.Equal(525u, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(FinancialReason.Sell, receipt.Reason);
        Assert.Equal(PropertyIndex, receipt.CounterpartyPropertyIndex);
    }

    [Fact]
    public async Task ReceiveForMortgage_CreditsPlayer_ReasonMortgage()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ReceiveForMortgage(engine, cache.Game.GetPlayer(P1)!, 30, PropertyIndex, CancellationToken.None);

        Assert.Equal(530u, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(FinancialReason.Mortgage, receipt.Reason);
        Assert.Equal(PropertyIndex, receipt.CounterpartyPropertyIndex);
    }

    [Fact]
    public async Task ReceiveCardPayout_FromBank_CreditsPlayer()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ReceiveCardPayout(engine, cache.Game.GetPlayer(P1)!, 75,
            TransactionCounterparty.Bank, null, CancellationToken.None);

        Assert.Equal(575u, Money(cache, P1));
        var receipt = SingleReceipt(cache);
        Assert.Equal(FinancialReason.CardPayout, receipt.Reason);
        Assert.Equal(TransactionCounterparty.Bank, receipt.Counterparty);
    }


    // ════════════════════════════════════════════════════════════════════
    //  Player-to-player (two mirrored receipts)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PayCardCharge_ToPlayer_MovesMoneyBothWays()
    {
        var cache = CreateCache(players: [Player(P1, money: 500), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayCardCharge(engine, cache.Game.GetPlayer(P1)!, 80,
            TransactionCounterparty.Player, cache.Game.GetPlayer(P2)!, CancellationToken.None);

        Assert.Equal(420u, Money(cache, P1));
        Assert.Equal(580u, Money(cache, P2));
    }

    [Fact]
    public async Task PayCardCharge_ToPlayer_EmitsMirroredReceipts()
    {
        var cache = CreateCache(players: [Player(P1, money: 500), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayCardCharge(engine, cache.Game.GetPlayer(P1)!, 80,
            TransactionCounterparty.Player, cache.Game.GetPlayer(P2)!, CancellationToken.None);

        var receipts = Receipts(cache);
        Assert.Equal(2, receipts.Count);

        var payer = receipts.Single(r => r.PlayerId == P1);
        Assert.Equal(-80, payer.Amount);
        Assert.Equal(TransactionCounterparty.Player, payer.Counterparty);
        Assert.Equal(P2, payer.CounterpartyPlayerId);

        var receiver = receipts.Single(r => r.PlayerId == P2);
        Assert.Equal(80, receiver.Amount);
        Assert.Equal(TransactionCounterparty.Player, receiver.Counterparty);
        Assert.Equal(P1, receiver.CounterpartyPlayerId);
    }

    [Fact]
    public async Task PayDiceBonus_SinglePayer_PayerLosesReceiverGains()
    {
        var cache = CreateCache(players: [Player(P1, money: 500), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        // P1 pays the dice-number bonus to P2 (the player whose number was rolled).
        await txn.PayDiceBonus(engine, cache.Game.GetPlayer(P1)!, cache.Game.GetPlayer(P2)!, CancellationToken.None);

        Assert.Equal(500u - RuleDictionary.DiceNumRolledBonus, Money(cache, P1));
        Assert.Equal(500u + RuleDictionary.DiceNumRolledBonus, Money(cache, P2));
    }

    [Fact]
    public async Task ProcessDealPayment_PositiveAmount_PlayerReceivesFromOther()
    {
        var cache = CreateCache(players: [Player(P1, money: 500), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ProcessDealPayment(engine, cache.Game.GetPlayer(P1)!, cache.Game.GetPlayer(P2)!, 120, CancellationToken.None);

        Assert.Equal(620u, Money(cache, P1));
        Assert.Equal(380u, Money(cache, P2));
        Assert.Equal(FinancialReason.Deal, Receipts(cache).First().Reason);
    }

    [Fact]
    public async Task ProcessDealPayment_NegativeAmount_PlayerPaysOther()
    {
        var cache = CreateCache(players: [Player(P1, money: 500), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ProcessDealPayment(engine, cache.Game.GetPlayer(P1)!, cache.Game.GetPlayer(P2)!, -120, CancellationToken.None);

        Assert.Equal(380u, Money(cache, P1));
        Assert.Equal(620u, Money(cache, P2));
    }


    // ════════════════════════════════════════════════════════════════════
    //  Rent (PayRent) — board + property driven
    // ════════════════════════════════════════════════════════════════════

    private static GameCacheModel CreateRentCache(
        PropertyState state = PropertyState.Owned,
        bool ownerInJail = false,
        GameRoundingRule rounding = GameRoundingRule.None)
        => CreateCache(
            rounding: rounding,
            players:
            [
                Player(P1, money: 500),
                Player(P2, orderId: 1, money: 500, boardIndex: ownerInJail ? IndexHelper.JailSpace : (ushort)0)
            ],
            properties: [Prop(PropertyIndex, P2, state)],
            board: RentBoard());

    [Fact]
    public async Task PayRent_OwnedProperty_PayerPaysOwner_PropertyIndexOnReceipts()
    {
        var cache = CreateRentCache();
        var engine = CreateEngine(cache);
        var txn = CreateService();

        // SINGLE rent for the fixture property is £2.
        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 2u, PropertyIndex, CancellationToken.None);

        Assert.Equal(498u, Money(cache, P1));
        Assert.Equal(502u, Money(cache, P2));

        var receipts = Receipts(cache);
        Assert.Equal(2, receipts.Count);
        Assert.All(receipts, r => Assert.Equal(FinancialReason.Rent, r.Reason));
        Assert.All(receipts, r => Assert.Equal(PropertyIndex, r.CounterpartyPropertyIndex));
    }

    [Fact]
    public async Task PayRent_UnownedProperty_NoOp()
    {
        var cache = CreateCache(
            players: [Player(P1, money: 500)],
            properties: [Prop(PropertyIndex, owner: null, PropertyState.NotOwned)],
            board: RentBoard());
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 2u, PropertyIndex, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task PayRent_MortgagedProperty_NoOp()
    {
        var cache = CreateRentCache(state: PropertyState.Mortgaged);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 2u, PropertyIndex, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));
        Assert.Equal(500u, Money(cache, P2));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task PayRent_ReservedProperty_NoOp()
    {
        var cache = CreateRentCache(state: PropertyState.Reserved);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 2u, PropertyIndex, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task PayRent_OwnerInJail_NoOp()
    {
        var cache = CreateRentCache(ownerInJail: true);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 2u, PropertyIndex, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));     // game-rules.md Default rule 2
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task PayRent_NonRentableSpace_NoOp()
    {
        // The tax space (index 4) is not rentable — PayRent should short-circuit.
        var cache = CreateCache(players: [Player(P1, money: 500)], board: RentBoard());
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 200, TaxIndex, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task PayRent_RentRoundsToZero_NoMovementNoReceipt()
    {
        // £2 SINGLE rent under "round to 50" resolves to 0 — and rent that
        // resolves to 0 stays 0 (no minimum-grid bump). No money moves.
        var cache = CreateRentCache(rounding: GameRoundingRule.To50);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayRent(engine, cache.Game.GetPlayer(P1)!, 2u, PropertyIndex, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));
        Assert.Equal(500u, Money(cache, P2));
        Assert.Empty(Receipts(cache));
    }


    // ════════════════════════════════════════════════════════════════════
    //  Rounding (MoneyHelper integration)
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PayTax_RoundsToNearestGrid_PerRoundingRule()
    {
        var cache = CreateCache(rounding: GameRoundingRule.To5, players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 23, CancellationToken.None);   // → 25

        Assert.Equal(475u, Money(cache, P1));
        Assert.Equal(-25, SingleReceipt(cache).Amount);
        Assert.Equal(25u, cache.Game.FreeParkingAmount);
    }

    [Fact]
    public async Task NonRentDebit_RoundingToZero_BumpsToMinimumGrid()
    {
        // £2 charge under "round to 10" rounds to 0 — a non-rent charge bumps
        // to one grid unit so it never silently vanishes.
        var cache = CreateCache(rounding: GameRoundingRule.To10,
            players: [Player(P1, money: 500), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayCardCharge(engine, cache.Game.GetPlayer(P1)!, 2,
            TransactionCounterparty.Player, cache.Game.GetPlayer(P2)!, CancellationToken.None);

        Assert.Equal(490u, Money(cache, P1));
        Assert.Equal(510u, Money(cache, P2));
        Assert.Equal(-10, Receipts(cache).Single(r => r.PlayerId == P1).Amount);
    }

    [Fact]
    public async Task ReceiveGoBonus_RoundsToGrid_PerRule()
    {
        var cache = CreateCache(rounding: GameRoundingRule.To5, players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.ReceiveGoBonus(engine, cache.Game.GetPlayer(P1)!, 23, CancellationToken.None);  // → 25

        Assert.Equal(525u, Money(cache, P1));
        Assert.Equal(25, SingleReceipt(cache).Amount);
    }


    // ════════════════════════════════════════════════════════════════════
    //  Shortfall flow
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Debit_InsufficientFunds_ShortfallAllowed_OpensShortfallPromptWithComputedFields()
    {
        var cache = CreateCache(players: [Player(P1, money: 50)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        var task = txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);

        var prompt = Assert.IsType<ShortfallPrompt>(await WaitForPendingPromptAsync(cache));
        Assert.Equal(200u, prompt.Cost);
        Assert.Equal(50u, prompt.PlayerBalance);
        Assert.Equal(150u, prompt.AmountOwed);
        Assert.Null(prompt.OwedToPlayerId);   // tax is owed to the pot, not a player

        // Resolve so the awaiting work doesn't dangle.
        Assert.True(engine.PromptProvider.TrySubmit(P1, cache.ConcurrencyStamp,
            new ShortfallResponse { PromptId = prompt.PromptId, Action = ShortfallAction.DeclareBankruptcy }));
        await task;
    }

    [Fact]
    public async Task Debit_InsufficientFunds_DeclareBankruptcy_AbandonsTransaction()
    {
        var cache = CreateCache(freeParking: 0, players: [Player(P1, money: 50)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        var task = txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);
        var prompt = await WaitForPendingPromptAsync(cache);
        engine.PromptProvider.TrySubmit(P1, cache.ConcurrencyStamp,
            new ShortfallResponse { PromptId = prompt.PromptId, Action = ShortfallAction.DeclareBankruptcy });
        await task;

        Assert.Equal(50u, Money(cache, P1));          // never debited
        Assert.Equal(0u, cache.Game.FreeParkingAmount);
        Assert.Empty(Receipts(cache));                // settling/terminal receipts belong to the sub-service
    }

    [Fact]
    public async Task Debit_InsufficientFunds_OwedToPlayer_ProposeDeal_AbandonsTransaction()
    {
        var cache = CreateCache(players: [Player(P1, money: 50), Player(P2, orderId: 1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        var task = txn.PayCardCharge(engine, cache.Game.GetPlayer(P1)!, 200,
            TransactionCounterparty.Player, cache.Game.GetPlayer(P2)!, CancellationToken.None);

        var prompt = Assert.IsType<ShortfallPrompt>(await WaitForPendingPromptAsync(cache));
        Assert.Equal(P2, prompt.OwedToPlayerId);      // creditor known → ProposeDeal is valid

        engine.PromptProvider.TrySubmit(P1, cache.ConcurrencyStamp,
            new ShortfallResponse { PromptId = prompt.PromptId, Action = ShortfallAction.ProposeDeal });
        await task;

        // The deal IS the settlement — the original card charge must not also apply.
        Assert.Equal(50u, Money(cache, P1));
        Assert.Equal(500u, Money(cache, P2));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task Debit_InsufficientFunds_ShortfallDisallowed_SilentNoOp()
    {
        var cache = CreateCache(players: [Player(P1, money: 50)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        // Purchase pre-gates affordability (Default rule 7) — an under-funded
        // call silently no-ops, no prompt.
        await txn.PurchaseProperty(engine, cache.Game.GetPlayer(P1)!, 200, PropertyIndex, CancellationToken.None);

        Assert.Null(cache.PendingPrompt);
        Assert.Equal(50u, Money(cache, P1));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task Debit_AmountEqualsBalance_AppliesWithoutPrompt()
    {
        var cache = CreateCache(players: [Player(P1, money: 200)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);

        Assert.Null(cache.PendingPrompt);
        Assert.Equal(0u, Money(cache, P1));
        Assert.Equal(200u, cache.Game.FreeParkingAmount);
    }


    // ════════════════════════════════════════════════════════════════════
    //  Zero amount & stamping invariants
    // ════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task ZeroAmount_NoOp_NoReceipt()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        await txn.PayTax(engine, cache.Game.GetPlayer(P1)!, 0, CancellationToken.None);

        Assert.Equal(500u, Money(cache, P1));
        Assert.Empty(Receipts(cache));
    }

    [Fact]
    public async Task SuccessfulMove_RestampsConcurrency()
    {
        var cache = CreateCache(players: [Player(P1, money: 500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();
        var stampBefore = cache.ConcurrencyStamp;

        await txn.ReceiveGoBonus(engine, cache.Game.GetPlayer(P1)!, 200, CancellationToken.None);

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }


    // ════════════════════════════════════════════════════════════════════
    //  🔴 Dice number bonus — the reported multi-payer bug
    //
    //  game-rules.md Dice Rolls rule 3: a player who rolls their OWN dice
    //  number collects £100 from the bank PLUS £100 from every other player.
    //  These tests mirror PlayerService.ResolveDiceNumber's exact call
    //  sequence against TransactionService.
    //
    //  Expected to FAIL until fixed: each Move ends with SaveChanges(), which
    //  nulls the cache working copy; re-reading players afterwards mints a new
    //  copy, leaving the roller reference held across the loop pointing at the
    //  orphaned previous copy. The +£100 credits land on that orphan and are
    //  lost on the next commit — so the roller ends with only the bank £100.
    // ════════════════════════════════════════════════════════════════════

    private static GameCacheModel CreateThreePlayerCache()
        => CreateCache(
            currentPlayerId: P1,
            players:
            [
                Player(P1, orderId: 0, money: 1500),   // the roller (rolled their own number)
                Player(P2, orderId: 1, money: 1500),
                Player(P3, orderId: 2, money: 1500)
            ]);

    [Fact]
    public async Task DiceNumberBonus_ResolveStyle_EachOtherPlayerPaysHundred()
    {
        var cache = CreateThreePlayerCache();
        var engine = CreateEngine(cache);
        var txn = CreateService();

        // Mirror ResolveDiceNumber: roller takes the bank bonus, then every
        // other player pays the roller £100.
        var roller = cache.Game.GetPlayer(P1)!;
        await txn.ReceiveDiceBonus(engine, roller, CancellationToken.None);
        foreach (var payer in cache.Game.GetPlayers(P1))
            await txn.PayDiceBonus(engine, payer, roller, CancellationToken.None);

        Assert.Equal(1500u - RuleDictionary.DiceNumRolledBonus, Money(cache, P2));
        Assert.Equal(1500u - RuleDictionary.DiceNumRolledBonus, Money(cache, P3));
    }

    [Fact]
    public async Task DiceNumberBonus_ResolveStyle_RollerCollectsBankBonusPlusFromEveryPayer()
    {
        var cache = CreateThreePlayerCache();
        var engine = CreateEngine(cache);
        var txn = CreateService();

        var roller = cache.Game.GetPlayer(P1)!;
        await txn.ReceiveDiceBonus(engine, roller, CancellationToken.None);
        foreach (var payer in cache.Game.GetPlayers(P1))
            await txn.PayDiceBonus(engine, payer, roller, CancellationToken.None);

        engine.Cache.SaveChanges();
        // £100 bank + £100 × 2 other players.
        const uint expected = 1500u + RuleDictionary.DiceNumRolledBonus + (RuleDictionary.DiceNumRolledBonus * 2);
        Assert.Equal(expected, Money(cache, P1));
    }

    [Fact]
    public async Task Move_CounterpartyReferenceHeldAcrossASaveChanges_StillCreditsCounterparty()
    {
        // Minimal, transaction-type-agnostic demonstration of the same root
        // cause: any counterparty reference obtained before a prior committing
        // Move is detached from the live working copy, so its credit is lost.
        var cache = CreateCache(players: [Player(P1, money: 1500), Player(P2, orderId: 1, money: 1500)]);
        var engine = CreateEngine(cache);
        var txn = CreateService();

        var receiver = cache.Game.GetPlayer(P2)!;                 // bound to working copy A
        await txn.ReceiveGoBonus(engine, receiver, 1, CancellationToken.None);   // commits → A detached
        var payer = cache.Game.GetPlayer(P1)!;                    // fresh working copy B; receiver now stale
        await txn.PayCardCharge(engine, payer, 100,
            TransactionCounterparty.Player, receiver, CancellationToken.None);

        Assert.Equal(1400u, Money(cache, P1));                    // payer debited (in the live copy) — passes
        Assert.Equal(1500u + 1u + 100u, Money(cache, P2));        // receiver should get the £100 — currently lost
    }
}
