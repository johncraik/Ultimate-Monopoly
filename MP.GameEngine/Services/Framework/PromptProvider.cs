using MP.GameEngine.Abstractions;
using MP.GameEngine.Models;
using MP.GameEngine.Models.Prompts;

namespace MP.GameEngine.Services.Framework;

/// <summary>
/// Default <see cref="IPromptProvider"/> implementation. One instance per
/// game cache — typically registered scoped against the cache that backs an
/// in-progress game.
/// </summary>
public sealed class PromptProvider : IPromptProvider
{
    private readonly GameCacheModel _cache;

    public PromptProvider(GameCacheModel cache)
    {
        _cache = cache;
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

        await using (ct.Register(() => tcs.TrySetCanceled(ct)))
        {
            var response = await tcs.Task.ConfigureAwait(false);
            return (TResponse)response;
        }
    }

    /// <inheritdoc />
    public bool TrySubmit(string submittingUserId, string concurrencyStamp, PromptResponse response)
    {
        if (_cache.ConcurrencyStamp != concurrencyStamp) return false;

        var pending = _cache.PendingPrompt;
        if (pending is null) return false;
        if (pending.Prompt.PromptId != response.PromptId) return false;
        if (!PromptValidator.Validate(pending.Prompt, response, submittingUserId, _cache)) return false;

        _cache.ClearPendingPrompt();
        pending.Tcs.TrySetResult(response);
        return true;
    }
}
