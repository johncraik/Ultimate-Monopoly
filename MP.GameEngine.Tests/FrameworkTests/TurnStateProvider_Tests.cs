using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Framework;

// ReSharper disable InconsistentNaming
namespace MP.GameEngine.Tests.FrameworkTests;

public class TurnStateProvider_Tests
{
    // ─── Fixtures ───────────────────────────────────────────────────────

    private const string PlayerId = "player-1";
    private const string OtherPlayerId = "player-2";
    private const string HostId = "host";
    private const ushort SafeBoardIndex = 5;   // any non-jail index

    private static GameCacheModel CreateCache(
        string currentPlayerId = PlayerId,
        bool currentPlayerInJail = false,
        ushort initialDoublesInRow = 0,
        ushort initialTriplesInRow = 0)
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
                CurrentPlayerId = currentPlayerId,
                TurnNumber = 1
            },
            Players =
            [
                new PlayerModel
                {
                    PlayerId = PlayerId,
                    OrderId = 0,
                    BoardIndex = currentPlayerId == PlayerId && currentPlayerInJail
                        ? IndexHelper.JailSpace
                        : SafeBoardIndex,
                    DoublesInRow = initialDoublesInRow,
                    TriplesInRow = initialTriplesInRow
                },
                new PlayerModel
                {
                    PlayerId = OtherPlayerId,
                    OrderId = 1,
                    BoardIndex = SafeBoardIndex
                }
            ]
        };

        var board = new Board("Test Board", new List<BoardSpace>());
        return new GameCacheModel(dto, game, board);
    }

    private static TurnStateProvider CreateProvider(GameCacheModel cache)
        => new(cache, new SnapshotServiceMock());

    private static (TurnStateProvider provider, SnapshotServiceMock snapshots) CreateProviderWithSnapshots(GameCacheModel cache)
    {
        var snapshots = new SnapshotServiceMock();
        return (new TurnStateProvider(cache, snapshots), snapshots);
    }

    /// <summary>
    /// Test double for <see cref="ISnapshotService"/>. Records every call
    /// and simulates the real impl's side-effect of writing back a fresh
    /// <c>CurrentTurnId</c> to the passed-in <c>GameModel.Metadata</c>.
    /// </summary>
    private class SnapshotServiceMock : ISnapshotService
    {
        public int CallCount { get; private set; }
        public List<GameModel> Calls { get; } = [];

        public Task CreateSnapshotAsync(GameModel game, bool completeTransaction = true)
        {
            CallCount++;
            Calls.Add(game);
            game.Metadata.CurrentTurnId = Guid.NewGuid().ToString();
            return Task.CompletedTask;
        }
    }

    /// <summary>Advances the cache to the requested state via the provider's own transitions.</summary>
    private static void AdvanceTo(TurnStateProvider provider, TurnState state)
    {
        switch (state)
        {
            case TurnState.StartOfTurn:
                return;
            case TurnState.PlayerRollMovement:
                provider.TransitionToRollPhase();
                return;
            case TurnState.ThirdDieMovement:
                provider.TransitionToRollPhase();
                provider.TransitionToThirdDie();
                return;
            case TurnState.EndOfTurn:
                provider.TransitionToRollPhase();
                provider.TransitionToEndOfTurn();
                return;
        }
    }

    /// <summary>No-op notifier — these tests don't assert on broadcasts.</summary>
    private sealed class NoOpNotifier : IEngineNotifier
    {
        public void PromptOpened(string gameId, Prompt prompt, string concurrencyStamp) { }
        public void PromptClosed(string gameId, string promptId, string concurrencyStamp) { }
        public void StateChanged(GameCacheModel cache) { }
    }

    /// <summary>Drops a prompt into the cache so IsEngineIdle returns false.</summary>
    private static void OpenPrompt(GameCacheModel cache)
    {
        var prompts = new PromptProvider(cache, new NoOpNotifier());
        _ = prompts.RequestAsync(new AcknowledgePrompt
        {
            PlayerId = PlayerId,
            Title = "x",
            Body = "x"
        });
    }


    // ─── CanPortfolioCommand ────────────────────────────────────────────

    [Fact]
    public void CanPortfolioCommand_AllConditionsMet_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanPortfolioCommand(PlayerId, PlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_NotCurrentPlayer_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache());
        Assert.False(provider.CanPortfolioCommand(OtherPlayerId, OtherPlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_PlayerInJail_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: true));
        Assert.False(provider.CanPortfolioCommand(PlayerId, PlayerId));
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    [InlineData(TurnState.EndOfTurn)]
    public void CanPortfolioCommand_WrongTurnState_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanPortfolioCommand(PlayerId, PlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanPortfolioCommand(PlayerId, PlayerId));
    }


    // ─── CanDeal ────────────────────────────────────────────────────────

    [Fact]
    public void CanDeal_StartOfTurn_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeal(PlayerId, PlayerId));
    }

    [Fact]
    public void CanDeal_EndOfTurn_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanDeal(PlayerId, PlayerId));
    }

    [Fact]
    public void CanDeal_AnyPlayer_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeal(OtherPlayerId, OtherPlayerId));
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void CanDeal_MidExecution_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanDeal(PlayerId, PlayerId));
    }

    [Fact]
    public void CanDeal_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanDeal(PlayerId, PlayerId));
    }


    // ─── CanLeaveJail ───────────────────────────────────────────────────

    [Fact]
    public void CanLeaveJail_AllConditionsMet_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: true));
        Assert.True(provider.CanLeaveJail(PlayerId, PlayerId));
    }

    [Fact]
    public void CanLeaveJail_PlayerNotInJail_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: false));
        Assert.False(provider.CanLeaveJail(PlayerId, PlayerId));
    }

    [Fact]
    public void CanLeaveJail_NotCurrentPlayer_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: true));
        // OtherPlayer isn't in jail in the fixture and isn't current — both fail.
        Assert.False(provider.CanLeaveJail(OtherPlayerId, OtherPlayerId));
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    [InlineData(TurnState.EndOfTurn)]
    public void CanLeaveJail_WrongTurnState_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache(currentPlayerInJail: true);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanLeaveJail(PlayerId, PlayerId));
    }

    [Fact]
    public void CanLeaveJail_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache(currentPlayerInJail: true);
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanLeaveJail(PlayerId, PlayerId));
    }


    // ─── CanEndTurn ─────────────────────────────────────────────────────

    [Fact]
    public void CanEndTurn_EndOfTurnByCurrentPlayer_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanEndTurn(PlayerId, PlayerId));
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void CanEndTurn_WrongTurnState_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanEndTurn(PlayerId, PlayerId));
    }

    [Fact]
    public void CanEndTurn_NotCurrentPlayer_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.False(provider.CanEndTurn(OtherPlayerId, OtherPlayerId));
    }

    [Fact]
    public void CanEndTurn_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);
        OpenPrompt(cache);

        Assert.False(provider.CanEndTurn(PlayerId, PlayerId));
    }


    // ─── CanDeclareBankruptcy ───────────────────────────────────────────

    [Fact]
    public void CanDeclareBankruptcy_StartOfTurn_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeclareBankruptcy(PlayerId, PlayerId));
    }

    [Fact]
    public void CanDeclareBankruptcy_EndOfTurn_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanDeclareBankruptcy(PlayerId, PlayerId));
    }

    [Fact]
    public void CanDeclareBankruptcy_AnyPlayer_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeclareBankruptcy(OtherPlayerId, OtherPlayerId));
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void CanDeclareBankruptcy_MidExecution_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanDeclareBankruptcy(PlayerId, PlayerId));
    }

    [Fact]
    public void CanDeclareBankruptcy_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanDeclareBankruptcy(PlayerId, PlayerId));
    }


    // ─── Host bypass (submitter acts for the named player) ──────────────

    [Fact]
    public void CanEndTurn_HostSubmitsForCurrentPlayer_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanEndTurn(PlayerId, HostId));
    }

    [Fact]
    public void CanEndTurn_UnrelatedPlayerSubmits_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        // OtherPlayer is neither the named player nor the host.
        Assert.False(provider.CanEndTurn(PlayerId, OtherPlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_HostSubmitsForCurrentPlayer_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanPortfolioCommand(PlayerId, HostId));
    }

    [Fact]
    public void CanDeclareBankruptcy_HostSubmitsForAnyPlayer_ReturnsTrue()
    {
        // Host declares on a non-current player's behalf at a turn boundary.
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeclareBankruptcy(OtherPlayerId, HostId));
    }


    // ─── TransitionToRollPhase ──────────────────────────────────────────

    [Fact]
    public void TransitionToRollPhase_FromStartOfTurn_SetsPlayerRollMovement()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);

        provider.TransitionToRollPhase();

        Assert.Equal(TurnState.PlayerRollMovement, cache.TurnState);
    }

    [Fact]
    public void TransitionToRollPhase_FromStartOfTurn_RestampsConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var stampBefore = cache.ConcurrencyStamp;

        provider.TransitionToRollPhase();

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    [InlineData(TurnState.EndOfTurn)]
    public void TransitionToRollPhase_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        Assert.Throws<InvalidOperationException>(provider.TransitionToRollPhase);
    }


    // ─── TransitionToThirdDie ───────────────────────────────────────────

    [Fact]
    public void TransitionToThirdDie_FromPlayerRollMovement_SetsThirdDieMovement()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.PlayerRollMovement);

        provider.TransitionToThirdDie();

        Assert.Equal(TurnState.ThirdDieMovement, cache.TurnState);
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.ThirdDieMovement)]
    [InlineData(TurnState.EndOfTurn)]
    public void TransitionToThirdDie_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        Assert.Throws<InvalidOperationException>(provider.TransitionToThirdDie);
    }


    // ─── TransitionToEndOfTurn ──────────────────────────────────────────

    [Fact]
    public void TransitionToEndOfTurn_FromPlayerRollMovement_SetsEndOfTurn()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.PlayerRollMovement);

        provider.TransitionToEndOfTurn();

        Assert.Equal(TurnState.EndOfTurn, cache.TurnState);
    }

    [Fact]
    public void TransitionToEndOfTurn_FromThirdDieMovement_SetsEndOfTurn()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.ThirdDieMovement);

        provider.TransitionToEndOfTurn();

        Assert.Equal(TurnState.EndOfTurn, cache.TurnState);
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.EndOfTurn)]
    public void TransitionToEndOfTurn_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        Assert.Throws<InvalidOperationException>(provider.TransitionToEndOfTurn);
    }


    // ─── TransitionToExtraTurn ──────────────────────────────────────────

    [Fact]
    public async Task TransitionToExtraTurn_FromEndOfTurn_SetsStartOfTurn()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(TurnState.StartOfTurn, cache.TurnState);
    }

    [Fact]
    public async Task TransitionToExtraTurn_DoesNotAdvanceCurrentPlayer()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(PlayerId, cache.Game.Metadata.CurrentPlayerId);
    }

    [Fact]
    public async Task TransitionToExtraTurn_BumpsTurnNumber()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var turnNumberBefore = cache.Game.Metadata.TurnNumber;
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(turnNumberBefore + 1, cache.Game.Metadata.TurnNumber);
    }

    [Fact]
    public async Task TransitionToExtraTurn_AfterDouble_BumpsDoublesInRow()
    {
        var cache = CreateCache(initialDoublesInRow: 1);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(2, cache.Game.Players.First(p => p.PlayerId == PlayerId).DoublesInRow);
    }

    [Fact]
    public async Task TransitionToExtraTurn_AfterDouble_ResetsTriplesInRow()
    {
        var cache = CreateCache(initialTriplesInRow: 2);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(0, cache.Game.Players.First(p => p.PlayerId == PlayerId).TriplesInRow);
    }

    [Fact]
    public async Task TransitionToExtraTurn_AfterTriple_BumpsTriplesInRow()
    {
        var cache = CreateCache(initialTriplesInRow: 1);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: true);

        Assert.Equal(2, cache.Game.Players.First(p => p.PlayerId == PlayerId).TriplesInRow);
    }

    [Fact]
    public async Task TransitionToExtraTurn_AfterTriple_ResetsDoublesInRow()
    {
        var cache = CreateCache(initialDoublesInRow: 2);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: true);

        Assert.Equal(0, cache.Game.Players.First(p => p.PlayerId == PlayerId).DoublesInRow);
    }

    [Fact]
    public async Task TransitionToExtraTurn_ClearsEvents()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        cache.AddEvent(new PlayerMovedReceipt { PlayerId = PlayerId });
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.Empty(cache.Events);
    }

    [Fact]
    public async Task TransitionToExtraTurn_CallsSnapshotService()
    {
        var cache = CreateCache();
        var (provider, snapshots) = CreateProviderWithSnapshots(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToExtraTurn(isTriple: false);

        // The captured GameModel reference is the working copy the
        // transition handed to the snapshot service; it carries the
        // bumped TurnNumber and the unchanged CurrentPlayerId. We don't
        // Assert.Same against cache.Game because the trailing
        // cache.SaveChanges() promotes that working copy into _game and
        // the next cache.Game access lazily creates a fresh clone.
        Assert.Equal(1, snapshots.CallCount);
        Assert.Equal(2u, snapshots.Calls[0].Metadata.TurnNumber);
        Assert.Equal(PlayerId, snapshots.Calls[0].Metadata.CurrentPlayerId);
    }

    [Fact]
    public async Task TransitionToExtraTurn_RestampsConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);
        var stampBefore = cache.ConcurrencyStamp;

        await provider.TransitionToExtraTurn(isTriple: false);

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public async Task TransitionToExtraTurn_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TransitionToExtraTurn(isTriple: false));
    }


    // ─── TransitionToNextPlayer ─────────────────────────────────────────

    [Fact]
    public async Task TransitionToNextPlayer_FromEndOfTurn_SetsStartOfTurn()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToNextPlayer();

        Assert.Equal(TurnState.StartOfTurn, cache.TurnState);
    }

    [Fact]
    public async Task TransitionToNextPlayer_AdvancesToNextOrderId()
    {
        // PlayerId has OrderId=0, OtherPlayerId has OrderId=1 — next from PlayerId is OtherPlayerId.
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToNextPlayer();

        Assert.Equal(OtherPlayerId, cache.Game.Metadata.CurrentPlayerId);
    }

    [Fact]
    public async Task TransitionToNextPlayer_WrapsAroundFromHighestOrderId()
    {
        // OtherPlayerId has OrderId=1 (highest); next should wrap to PlayerId (OrderId=0).
        var cache = CreateCache(currentPlayerId: OtherPlayerId);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToNextPlayer();

        Assert.Equal(PlayerId, cache.Game.Metadata.CurrentPlayerId);
    }

    [Fact]
    public async Task TransitionToNextPlayer_BumpsTurnNumber()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var turnNumberBefore = cache.Game.Metadata.TurnNumber;
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToNextPlayer();

        Assert.Equal(turnNumberBefore + 1, cache.Game.Metadata.TurnNumber);
    }

    [Fact]
    public async Task TransitionToNextPlayer_ClearsEvents()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        cache.AddEvent(new PlayerMovedReceipt { PlayerId = PlayerId });
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToNextPlayer();

        Assert.Empty(cache.Events);
    }

    [Fact]
    public async Task TransitionToNextPlayer_CallsSnapshotService()
    {
        var cache = CreateCache();
        var (provider, snapshots) = CreateProviderWithSnapshots(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        await provider.TransitionToNextPlayer();

        // See TransitionToExtraTurn_CallsSnapshotService for why we
        // assert on captured Metadata values rather than reference equality.
        Assert.Equal(1, snapshots.CallCount);
        Assert.Equal(2u, snapshots.Calls[0].Metadata.TurnNumber);
        Assert.Equal(OtherPlayerId, snapshots.Calls[0].Metadata.CurrentPlayerId);
    }

    [Fact]
    public async Task TransitionToNextPlayer_RestampsConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);
        var stampBefore = cache.ConcurrencyStamp;

        await provider.TransitionToNextPlayer();

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public async Task TransitionToNextPlayer_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => provider.TransitionToNextPlayer());
    }
}