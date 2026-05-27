using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Framework;

// ReSharper disable InconsistentNaming
namespace MP.GameEngine.Tests.FrameworkTests;

public class EventEmitter_Tests
{
    // ─── Fixtures ───────────────────────────────────────────────────────

    private const string PlayerId = "player-1";
    private const string HostId = "host";
    private const uint InitialTurnNumber = 7;

    private static GameCacheModel CreateCache(uint turnNumber = InitialTurnNumber)
    {
        var dto = new GameDTO(
            id: "game-1",
            name: "Test Game",
            boardId: "board-1",
            roundingRule: GameRoundingRule.None,
            hostPlayerId: HostId,
            state: GameState.InPlay,
            outcome: GameOutcome.None);

        var game = new GameModel
        {
            Metadata = new TurnMetadata
            {
                CurrentTurnId = "turn-1",
                CurrentPlayerId = PlayerId,
                TurnNumber = turnNumber
            },
            Players =
            [
                new PlayerModel { PlayerId = PlayerId, BoardIndex = 5 }
            ]
        };

        var board = new Board("Test Board", new List<BoardSpace>());
        return new GameCacheModel(dto, game, board);
    }

    private static EventEmitter CreateEmitter(GameCacheModel cache) => new(cache);

    private static PlayerMovedReceipt CreateMoveReceipt(string playerId = PlayerId) =>
        new() { PlayerId = playerId };

    private static FinancialTransactionReceipt CreateMoneyReceipt(long amount = -50) =>
        new()
        {
            PlayerId = PlayerId,
            Amount = amount,
            Reason = FinancialReason.Rent,
            Counterparty = TransactionCounterparty.Player,
            CounterpartyPlayerId = "other"
        };


    // ─── Forwarding ─────────────────────────────────────────────────────

    [Fact]
    public void Emit_AddsReceiptToCacheEventsList()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);
        var receipt = CreateMoveReceipt();

        emitter.Emit(receipt);

        Assert.Single(cache.Events);
        Assert.Same(receipt, cache.Events[0]);
    }

    [Fact]
    public void Emit_PreservesProducerSetPlayerId()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);
        var receipt = CreateMoveReceipt(playerId: "different-player");

        emitter.Emit(receipt);

        Assert.Equal("different-player", cache.Events[0].PlayerId);
    }


    // ─── Bookkeeping: TurnNumber ────────────────────────────────────────

    [Fact]
    public void Emit_AssignsTurnNumberFromCacheMetadata()
    {
        var cache = CreateCache(turnNumber: 42);
        var emitter = CreateEmitter(cache);

        emitter.Emit(CreateMoveReceipt());

        Assert.Equal(42u, cache.Events[0].TurnNumber);
    }

    [Fact]
    public void Emit_AllReceiptsInSameTurnShareTurnNumber()
    {
        var cache = CreateCache(turnNumber: 3);
        var emitter = CreateEmitter(cache);

        emitter.Emit(CreateMoveReceipt());
        emitter.Emit(CreateMoneyReceipt());
        emitter.Emit(CreateMoveReceipt());

        Assert.All(cache.Events, r => Assert.Equal(3u, r.TurnNumber));
    }


    // ─── Bookkeeping: SequenceIndex ─────────────────────────────────────

    [Fact]
    public void Emit_FirstReceipt_GetsSequenceIndexZero()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);

        emitter.Emit(CreateMoveReceipt());

        Assert.Equal(0, cache.Events[0].SequenceIndex);
    }

    [Fact]
    public void Emit_SubsequentReceipts_GetIncrementingSequenceIndex()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);

        emitter.Emit(CreateMoveReceipt());
        emitter.Emit(CreateMoneyReceipt());
        emitter.Emit(CreateMoveReceipt());

        Assert.Equal(0, cache.Events[0].SequenceIndex);
        Assert.Equal(1, cache.Events[1].SequenceIndex);
        Assert.Equal(2, cache.Events[2].SequenceIndex);
    }

    [Fact]
    public void Emit_AfterClearEvents_SequenceIndexResetsToZero()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);
        emitter.Emit(CreateMoveReceipt());
        emitter.Emit(CreateMoveReceipt());

        cache.ClearEvents();
        emitter.Emit(CreateMoveReceipt());

        Assert.Single(cache.Events);
        Assert.Equal(0, cache.Events[0].SequenceIndex);
    }


    // ─── Ordering & Mixed Types ─────────────────────────────────────────

    [Fact]
    public void Emit_PreservesEmissionOrder()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);
        var first = CreateMoveReceipt();
        var second = CreateMoneyReceipt();
        var third = CreateMoveReceipt();

        emitter.Emit(first);
        emitter.Emit(second);
        emitter.Emit(third);

        Assert.Same(first, cache.Events[0]);
        Assert.Same(second, cache.Events[1]);
        Assert.Same(third, cache.Events[2]);
    }

    [Fact]
    public void Emit_DifferentReceiptTypes_AllStoredPolymorphically()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);

        emitter.Emit(new PlayerMovedReceipt { PlayerId = PlayerId });
        emitter.Emit(new PlayerEnteredJailReceipt { PlayerId = PlayerId });
        emitter.Emit(new DiceRollReceipt(PlayerId, new DiceRoll(1)));

        Assert.Equal(3, cache.Events.Count);
        Assert.IsType<PlayerMovedReceipt>(cache.Events[0]);
        Assert.IsType<PlayerEnteredJailReceipt>(cache.Events[1]);
        Assert.IsType<DiceRollReceipt>(cache.Events[2]);
    }


    // ─── Concurrency stamp ──────────────────────────────────────────────

    [Fact]
    public void Emit_RestampsConcurrency()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);
        var stampBefore = cache.ConcurrencyStamp;

        emitter.Emit(CreateMoveReceipt());

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Fact]
    public void Emit_EachEmission_RestampsAgain()
    {
        var cache = CreateCache();
        var emitter = CreateEmitter(cache);

        emitter.Emit(CreateMoveReceipt());
        var stampAfterFirst = cache.ConcurrencyStamp;

        emitter.Emit(CreateMoveReceipt());

        Assert.NotEqual(stampAfterFirst, cache.ConcurrencyStamp);
    }
}
