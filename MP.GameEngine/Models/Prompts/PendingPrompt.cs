namespace MP.GameEngine.Models.Prompts;

/// <summary>
/// In-memory wrapper for the currently open prompt. Pairs the public
/// <see cref="Prompt"/> with the framework-internal
/// <see cref="TaskCompletionSource{TResult}"/> that resolves it when a valid
/// response arrives. Lives in <c>GameCacheModel</c> and does not survive a
/// server restart — see <c>design-docs/choice-events.md</c> §1.
/// </summary>
public sealed class PendingPrompt
{
    public Prompt Prompt { get; }

    /// <summary>
    /// Resolved by the prompt provider on a valid submission, cancelled when
    /// the engine's cancellation token fires. Internal to the framework — no
    /// outside caller should touch it.
    /// </summary>
    internal TaskCompletionSource<PromptResponse> Tcs { get; }

    internal PendingPrompt(Prompt prompt, TaskCompletionSource<PromptResponse> tcs)
    {
        Prompt = prompt;
        Tcs = tcs;
    }
}
