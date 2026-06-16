namespace MP.GameEngine.Models.Cards.Actions;

/// <summary>
/// An action that does nothing when resolved. For cards whose entire effect is carried by their
/// card-level <c>SuppressDefault</c> metadata — e.g. "you receive no cash on your next visit to free
/// parking", which only suppresses the FP money-take when the trigger layer fires it — this gives the
/// card the ≥1-action a group structurally needs without performing any money / movement / state
/// change of its own. See cards-design.md §3.
/// </summary>
public sealed class NoOpAction : CardAction
{
}