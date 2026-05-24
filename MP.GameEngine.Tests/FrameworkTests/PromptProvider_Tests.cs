using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Framework;

// ReSharper disable InconsistentNaming
namespace MP.GameEngine.Tests.FrameworkTests;

public class PromptProvider_Tests
{
    // ─── Fixtures ───────────────────────────────────────────────────────

    private const string PlayerId = "player-1";
    private const string HostId = "host";
    private const string SomeoneElseId = "stranger";

    private static GameCacheModel CreateCache(string hostPlayerId = HostId, string currentPlayerId = PlayerId)
    {
        var dto = new GameDTO(
            id: "game-1",
            name: "Test Game",
            boardId: "board-1",
            roundingRule: GameRoundingRule.None,
            hostPlayerId: hostPlayerId,
            state: GameState.InPlay,
            outcome: GameOutcome.None);

        var game = new GameModel
        {
            Metadata = new TurnMetadata
            {
                CurrentTurnId = "turn-1",
                CurrentPlayerId = currentPlayerId,
                TurnNumber = 1
            }
        };

        var board = new Board("Test Board", new List<BoardSpace>());
        return new GameCacheModel(dto, game, board);
    }

    private static PromptProvider CreateProvider(GameCacheModel cache) => new(cache);

    private static AcknowledgePrompt CreateAckPrompt(string playerId = PlayerId) =>
        new() { PlayerId = playerId, Title = "Test", Body = "Test body" };

    private static AcknowledgeResponse CreateAckResponse(string promptId) =>
        new() { PromptId = promptId };


    // ─── RequestAsync — setup (synchronous behaviour before awaiting) ────

    [Fact]
    public void RequestAsync_NoPriorPending_SetsPendingPromptOnCache()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();

        _ = provider.RequestAsync(prompt);

        Assert.NotNull(cache.PendingPrompt);
        Assert.Same(prompt, cache.PendingPrompt.Prompt);
    }

    [Fact]
    public void RequestAsync_NoPriorPending_RestampsConcurrency()
    {
        var cache = CreateCache();
        var stampBefore = cache.ConcurrencyStamp;
        var provider = CreateProvider(cache);

        _ = provider.RequestAsync(CreateAckPrompt());

        Assert.NotEqual(stampBefore, cache.ConcurrencyStamp);
    }

    [Fact]
    public async Task RequestAsync_PromptAlreadyPending_Throws()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        _ = provider.RequestAsync(CreateAckPrompt());

        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            provider.RequestAsync(CreateAckPrompt()));
    }


    // ─── RequestAsync — async resolution ────────────────────────────────

    [Fact]
    public async Task RequestAsync_ValidSubmission_ReturnsTypedResponse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();

        var task = provider.RequestAsync(prompt);
        var stamp = cache.ConcurrencyStamp;
        var response = CreateAckResponse(prompt.PromptId);

        Assert.True(provider.TrySubmit(PlayerId, stamp, response));
        var result = await task;

        Assert.IsType<AcknowledgeResponse>(result);
        Assert.Equal(prompt.PromptId, result.PromptId);
    }

    [Fact]
    public async Task RequestAsync_NoSubmission_DoesNotComplete()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);

        var task = provider.RequestAsync(CreateAckPrompt());
        var winner = await Task.WhenAny(task, Task.Delay(50));

        Assert.NotSame(task, winner);
    }

    [Fact]
    public async Task RequestAsync_TokenPreCancelled_ThrowsOperationCanceled()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            provider.RequestAsync(CreateAckPrompt(), cts.Token));
    }

    [Fact]
    public async Task RequestAsync_TokenCancelledMidAwait_ThrowsOperationCanceled()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        using var cts = new CancellationTokenSource();

        var task = provider.RequestAsync(CreateAckPrompt(), cts.Token);
        await cts.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
    }


    // ─── TrySubmit — failure paths (return false, no side effects) ──────

    [Fact]
    public void TrySubmit_StaleStamp_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        var result = provider.TrySubmit(PlayerId, "not-the-real-stamp", CreateAckResponse(prompt.PromptId));

        Assert.False(result);
    }

    [Fact]
    public void TrySubmit_NoPromptPending_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);

        var result = provider.TrySubmit(PlayerId, cache.ConcurrencyStamp, CreateAckResponse("any-id"));

        Assert.False(result);
    }

    [Fact]
    public void TrySubmit_PromptIdMismatch_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        _ = provider.RequestAsync(CreateAckPrompt());

        var result = provider.TrySubmit(
            PlayerId, cache.ConcurrencyStamp, CreateAckResponse("wrong-prompt-id"));

        Assert.False(result);
    }

    [Fact]
    public void TrySubmit_WrongResponseType_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        var wrongType = new DiceRollResponse { PromptId = prompt.PromptId, Die1 = 1 };

        var result = provider.TrySubmit(PlayerId, cache.ConcurrencyStamp, wrongType);

        Assert.False(result);
    }

    [Fact]
    public void TrySubmit_UnauthorizedSubmitter_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        var result = provider.TrySubmit(
            SomeoneElseId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        Assert.False(result);
    }

    [Fact]
    public void TrySubmit_StaleStamp_DoesNotClearPending()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        provider.TrySubmit(PlayerId, "stale", CreateAckResponse(prompt.PromptId));

        Assert.NotNull(cache.PendingPrompt);
    }

    [Fact]
    public void TrySubmit_PromptIdMismatch_DoesNotClearPending()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        _ = provider.RequestAsync(CreateAckPrompt());

        provider.TrySubmit(PlayerId, cache.ConcurrencyStamp, CreateAckResponse("wrong-id"));

        Assert.NotNull(cache.PendingPrompt);
    }

    [Fact]
    public void TrySubmit_UnauthorizedSubmitter_DoesNotClearPending()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        provider.TrySubmit(SomeoneElseId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        Assert.NotNull(cache.PendingPrompt);
    }

    [Fact]
    public void TrySubmit_AnyFailure_DoesNotRestampConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);
        var stampAfterOpen = cache.ConcurrencyStamp;

        provider.TrySubmit(SomeoneElseId, stampAfterOpen, CreateAckResponse(prompt.PromptId));

        Assert.Equal(stampAfterOpen, cache.ConcurrencyStamp);
    }

    [Fact]
    public async Task TrySubmit_Failure_LeavesAwaitingTaskUnresolved()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        var task = provider.RequestAsync(prompt);

        provider.TrySubmit(SomeoneElseId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        var winner = await Task.WhenAny(task, Task.Delay(50));
        Assert.NotSame(task, winner);
    }


    // ─── TrySubmit — success path ───────────────────────────────────────

    [Fact]
    public void TrySubmit_ValidSubmission_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        var result = provider.TrySubmit(
            PlayerId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        Assert.True(result);
    }

    [Fact]
    public void TrySubmit_ValidSubmission_ClearsPendingPrompt()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        provider.TrySubmit(PlayerId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        Assert.Null(cache.PendingPrompt);
    }

    [Fact]
    public void TrySubmit_ValidSubmission_RestampsConcurrency()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);
        var stampAfterOpen = cache.ConcurrencyStamp;

        provider.TrySubmit(PlayerId, stampAfterOpen, CreateAckResponse(prompt.PromptId));

        Assert.NotEqual(stampAfterOpen, cache.ConcurrencyStamp);
    }

    [Fact]
    public async Task TrySubmit_ValidSubmission_ResolvesAwaitingTaskWithSubmittedResponse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        var task = provider.RequestAsync(prompt);
        var response = CreateAckResponse(prompt.PromptId);

        provider.TrySubmit(PlayerId, cache.ConcurrencyStamp, response);
        var result = await task;

        Assert.Same(response, result);
    }

    [Fact]
    public void TrySubmit_HostSubmitsOnPlayersBehalf_ReturnsTrue()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);

        var result = provider.TrySubmit(
            HostId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        Assert.True(result);
    }


    // ─── Interaction — after success / replay ───────────────────────────

    [Fact]
    public void TrySubmit_SecondSubmitWithOldStampAfterSuccess_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);
        var stampBeforeSubmit = cache.ConcurrencyStamp;

        provider.TrySubmit(PlayerId, stampBeforeSubmit, CreateAckResponse(prompt.PromptId));

        var second = provider.TrySubmit(PlayerId, stampBeforeSubmit, CreateAckResponse(prompt.PromptId));

        Assert.False(second);
    }

    [Fact]
    public void TrySubmit_OldPromptIdAfterNewPromptOpened_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var first = CreateAckPrompt();
        _ = provider.RequestAsync(first);
        provider.TrySubmit(PlayerId, cache.ConcurrencyStamp, CreateAckResponse(first.PromptId));

        var second = CreateAckPrompt();
        _ = provider.RequestAsync(second);

        var result = provider.TrySubmit(
            PlayerId, cache.ConcurrencyStamp, CreateAckResponse(first.PromptId));

        Assert.False(result);
    }


    // ─── Interaction — after cancellation ───────────────────────────────

    [Fact]
    public async Task RequestAsync_Cancellation_ClearsPendingPrompt()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        using var cts = new CancellationTokenSource();

        var task = provider.RequestAsync(CreateAckPrompt(), cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        Assert.Null(cache.PendingPrompt);
    }

    [Fact]
    public async Task TrySubmit_AfterCancellation_ReturnsFalse()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        using var cts = new CancellationTokenSource();
        var prompt = CreateAckPrompt();

        var task = provider.RequestAsync(prompt, cts.Token);
        await cts.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);

        var result = provider.TrySubmit(
            PlayerId, cache.ConcurrencyStamp, CreateAckResponse(prompt.PromptId));

        Assert.False(result);
    }


    // ─── Concurrent submissions ─────────────────────────────────────────

    [Fact]
    public async Task TrySubmit_ConcurrentValidSubmissions_OnlyFirstWins()
    {
        var cache = CreateCache();
        var provider = CreateProvider(cache);
        var prompt = CreateAckPrompt();
        _ = provider.RequestAsync(prompt);
        var stamp = cache.ConcurrencyStamp;

        // Race two valid submissions at the same stamp. Exactly one should win.
        var a = Task.Run(() => provider.TrySubmit(PlayerId, stamp, CreateAckResponse(prompt.PromptId)));
        var b = Task.Run(() => provider.TrySubmit(PlayerId, stamp, CreateAckResponse(prompt.PromptId)));

        var results = await Task.WhenAll(a, b);
        Assert.Single(results, true);
        Assert.Single(results, false);
    }
}