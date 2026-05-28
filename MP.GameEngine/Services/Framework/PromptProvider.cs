using MP.GameEngine.Abstractions;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Services.Framework;

/// <summary>
/// Default <see cref="IPromptProvider"/> implementation. One instance per
/// game cache — typically registered scoped against the cache that backs an
/// in-progress game.
/// </summary>
public sealed class PromptProvider : IPromptProvider
{
    private readonly GameCacheModel _cache;
    private readonly IEngineNotifier _notifier;

    public PromptProvider(GameCacheModel cache, IEngineNotifier notifier)
    {
        _cache = cache;
        _notifier = notifier;
    }

    /// <inheritdoc />
    public async Task<TResponse> RequestAsync<TResponse>(
        Prompt<TResponse> prompt,
        CancellationToken ct = default)
        where TResponse : PromptResponse
    {
        var tcs = new TaskCompletionSource<PromptResponse>(
            TaskCreationOptions.RunContinuationsAsynchronously);

        _cache.SetPendingPrompt(new PendingPrompt(prompt, tcs));
        _notifier.PromptOpened(_cache.GameId, prompt, _cache.ConcurrencyStamp);
        _notifier.StateChanged(_cache);

        try
        {
            await using (ct.Register(() => tcs.TrySetCanceled(ct)))
            {
                var response = await tcs.Task.ConfigureAwait(false);
                return (TResponse)response;
            }
        }
        finally
        {
            // Defensive cleanup: on the success path TrySubmit has already
            // cleared the pending prompt, so this no-ops. On cancellation /
            // exception, the pending prompt is still ours and would otherwise
            // be stranded — clear it (only if it really is ours; some other
            // prompt could theoretically have replaced it under future code
            // changes). ClearPendingPrompt re-stamps the concurrency stamp,
            // so any racing TrySubmit fails as stale.
            if (_cache.PendingPrompt is { } pending && pending.Prompt.PromptId == prompt.PromptId)
            {
                _cache.ClearPendingPrompt();
                _notifier.PromptClosed(_cache.GameId, prompt.PromptId, _cache.ConcurrencyStamp);
            }
        }
    }

    public async Task<AcknowledgeResponse> Acknowledge(string playerId, string title, string body, TimeSpan? timeout = null, CancellationToken ct = default)
    {
        var promptId = Guid.NewGuid().ToString();
        timeout ??= TimeSpan.FromSeconds(30);
        return await RequestAsync(new AcknowledgePrompt
        {
            PromptId = promptId,
            PlayerId = playerId,
            Title = title,
            Body = body,
            Timeout = timeout,
            DefaultResponse = new AcknowledgeResponse { PromptId = promptId }
        }, ct);
    }
    
    /// <inheritdoc />
    public bool TrySubmit(string submittingUserId, string concurrencyStamp, PromptResponse response)
    {
        if (_cache.ConcurrencyStamp != concurrencyStamp) return false;

        var pending = _cache.PendingPrompt;
        if (pending is null) return false;
        if (pending.Prompt.PromptId != response.PromptId) return false;
        if (!PromptValidator.Validate(pending.Prompt, response, submittingUserId, _cache)) return false;

        // TrySetResult is the atomic gate — only one of any concurrent
        // submitters can transition the TCS to the completed state. If we
        // lose the race, the TCS was already resolved (by a winning
        // submitter, or by cancellation) and we report false.
        if (!pending.Tcs.TrySetResult(response)) return false;

        _cache.ClearPendingPrompt();
        _notifier.PromptClosed(_cache.GameId, response.PromptId, _cache.ConcurrencyStamp);
        return true;
    }
}
