using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.EventReceipts;
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
                    BoardIndex = currentPlayerId == PlayerId && currentPlayerInJail
                        ? IndexHelper.JailSpace
                        : SafeBoardIndex,
                    DoublesInRow = initialDoublesInRow,
                    TriplesInRow = initialTriplesInRow
                },
                new PlayerModel
                {
                    PlayerId = OtherPlayerId,
                    BoardIndex = SafeBoardIndex
                }
            ]
        };

        var board = new Board("Test Board", new List<BoardSpace>());
        return new GameCacheModel(dto, game, board);
    }

    private static TurnStateProvider CreateProvider(GameCacheModel cache) => new(cache);

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

    /// <summary>Drops a prompt into the cache so IsEngineIdle returns false.</summary>
    private static void OpenPrompt(GameCacheModel cache)
    {
        var prompts = new PromptProvider(cache);
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
        Assert.True(provider.CanPortfolioCommand(PlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_NotCurrentPlayer_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache());
        Assert.False(provider.CanPortfolioCommand(OtherPlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_PlayerInJail_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: true));
        Assert.False(provider.CanPortfolioCommand(PlayerId));
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

        Assert.False(provider.CanPortfolioCommand(PlayerId));
    }

    [Fact]
    public void CanPortfolioCommand_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanPortfolioCommand(PlayerId));
    }


    // ─── CanDeal ────────────────────────────────────────────────────────

    [Fact]
    public void CanDeal_StartOfTurn_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeal(PlayerId));
    }

    [Fact]
    public void CanDeal_EndOfTurn_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanDeal(PlayerId));
    }

    [Fact]
    public void CanDeal_AnyPlayer_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeal(OtherPlayerId));
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void CanDeal_MidExecution_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanDeal(PlayerId));
    }

    [Fact]
    public void CanDeal_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanDeal(PlayerId));
    }


    // ─── CanLeaveJail ───────────────────────────────────────────────────

    [Fact]
    public void CanLeaveJail_AllConditionsMet_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: true));
        Assert.True(provider.CanLeaveJail(PlayerId));
    }

    [Fact]
    public void CanLeaveJail_PlayerNotInJail_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: false));
        Assert.False(provider.CanLeaveJail(PlayerId));
    }

    [Fact]
    public void CanLeaveJail_NotCurrentPlayer_ReturnsFalse()
    {
        var provider = CreateProvider(CreateCache(currentPlayerInJail: true));
        // OtherPlayer isn't in jail in the fixture and isn't current — both fail.
        Assert.False(provider.CanLeaveJail(OtherPlayerId));
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

        Assert.False(provider.CanLeaveJail(PlayerId));
    }

    [Fact]
    public void CanLeaveJail_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache(currentPlayerInJail: true);
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanLeaveJail(PlayerId));
    }


    // ─── CanEndTurn ─────────────────────────────────────────────────────

    [Fact]
    public void CanEndTurn_EndOfTurnByCurrentPlayer_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanEndTurn(PlayerId));
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

        Assert.False(provider.CanEndTurn(PlayerId));
    }

    [Fact]
    public void CanEndTurn_NotCurrentPlayer_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.False(provider.CanEndTurn(OtherPlayerId));
    }

    [Fact]
    public void CanEndTurn_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);
        OpenPrompt(cache);

        Assert.False(provider.CanEndTurn(PlayerId));
    }


    // ─── CanDeclareBankruptcy ───────────────────────────────────────────

    [Fact]
    public void CanDeclareBankruptcy_StartOfTurn_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeclareBankruptcy(PlayerId));
    }

    [Fact]
    public void CanDeclareBankruptcy_EndOfTurn_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        Assert.True(provider.CanDeclareBankruptcy(PlayerId));
    }

    [Fact]
    public void CanDeclareBankruptcy_AnyPlayer_ReturnsTrue()
    {
        var provider = CreateProvider(CreateCache());
        Assert.True(provider.CanDeclareBankruptcy(OtherPlayerId));
    }

    [Theory]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void CanDeclareBankruptcy_MidExecution_ReturnsFalse(TurnState state)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, state);

        Assert.False(provider.CanDeclareBankruptcy(PlayerId));
    }

    [Fact]
    public void CanDeclareBankruptcy_PromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        OpenPrompt(cache);

        Assert.False(provider.CanDeclareBankruptcy(PlayerId));
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
    public void TransitionToExtraTurn_FromEndOfTurn_SetsStartOfTurn()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(TurnState.StartOfTurn, cache.TurnState);
    }

    [Fact]
    public void TransitionToExtraTurn_DoesNotAdvanceCurrentPlayer()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(PlayerId, cache.Game.Metadata.CurrentPlayerId);
    }

    [Fact]
    public void TransitionToExtraTurn_DoesNotBumpTurnNumber()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var turnNumberBefore = cache.Game.Metadata.TurnNumber;
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(turnNumberBefore, cache.Game.Metadata.TurnNumber);
    }

    [Fact]
    public void TransitionToExtraTurn_AfterDouble_BumpsDoublesInRow()
    {
        var cache = CreateCache(initialDoublesInRow: 1);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(2, cache.Game.Players.First(p => p.PlayerId == PlayerId).DoublesInRow);
    }

    [Fact]
    public void TransitionToExtraTurn_AfterDouble_ResetsTriplesInRow()
    {
        var cache = CreateCache(initialTriplesInRow: 2);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.Equal(0, cache.Game.Players.First(p => p.PlayerId == PlayerId).TriplesInRow);
    }

    [Fact]
    public void TransitionToExtraTurn_AfterTriple_BumpsTriplesInRow()
    {
        var cache = CreateCache(initialTriplesInRow: 1);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: true);

        Assert.Equal(2, cache.Game.Players.First(p => p.PlayerId == PlayerId).TriplesInRow);
    }

    [Fact]
    public void TransitionToExtraTurn_AfterTriple_ResetsDoublesInRow()
    {
        var cache = CreateCache(initialDoublesInRow: 2);
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: true);

        Assert.Equal(0, cache.Game.Players.First(p => p.PlayerId == PlayerId).DoublesInRow);
    }

    [Fact]
    public void TransitionToExtraTurn_ClearsEvents()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        cache.AddEvent(new PlayerMovedReceipt { PlayerId = PlayerId });
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.Empty(cache.Events);
    }

    [Fact]
    public void TransitionToExtraTurn_ReturnsSnapshot()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        var snapshot = provider.TransitionToExtraTurn(isTriple: false);

        Assert.NotNull(snapshot);
        Assert.Same(cache.Game, snapshot);
    }

    [Fact]
    public void TransitionToExtraTurn_RestampsConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);
        var stampBefore = cache.ConcurrencyStamp;

        provider.TransitionToExtraTurn(isTriple: false);

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void TransitionToExtraTurn_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        Assert.Throws<InvalidOperationException>(() => provider.TransitionToExtraTurn(isTriple: false));
    }


    // ─── TransitionToNextPlayer ─────────────────────────────────────────
    //
    // AdvancePlayer is a TODO stub — these tests cover the framework behaviour
    // (state transition, commit, event clearing, snapshot return). The
    // player-advancement assertions (CurrentPlayerId rotates, TurnNumber++)
    // are deliberately not here; they'll be added when AdvancePlayer is
    // implemented.

    [Fact]
    public void TransitionToNextPlayer_FromEndOfTurn_SetsStartOfTurn()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToNextPlayer();

        Assert.Equal(TurnState.StartOfTurn, cache.TurnState);
    }

    [Fact]
    public void TransitionToNextPlayer_ClearsEvents()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        cache.AddEvent(new PlayerMovedReceipt { PlayerId = PlayerId });
        AdvanceTo(provider, TurnState.EndOfTurn);

        provider.TransitionToNextPlayer();

        Assert.Empty(cache.Events);
    }

    [Fact]
    public void TransitionToNextPlayer_ReturnsSnapshot()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);

        var snapshot = provider.TransitionToNextPlayer();

        Assert.NotNull(snapshot);
        Assert.Same(cache.Game, snapshot);
    }

    [Fact]
    public void TransitionToNextPlayer_RestampsConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, TurnState.EndOfTurn);
        var stampBefore = cache.ConcurrencyStamp;

        provider.TransitionToNextPlayer();

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Theory]
    [InlineData(TurnState.StartOfTurn)]
    [InlineData(TurnState.PlayerRollMovement)]
    [InlineData(TurnState.ThirdDieMovement)]
    public void TransitionToNextPlayer_FromWrongState_Throws(TurnState wrongState)
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        AdvanceTo(provider, wrongState);

        Assert.Throws<InvalidOperationException>(() => provider.TransitionToNextPlayer());
    }
}