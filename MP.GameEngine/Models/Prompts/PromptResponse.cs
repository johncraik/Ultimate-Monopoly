using System.Text.Json.Serialization;
using MP.GameEngine.Models.Prompts.PromptTypes.Responses;

namespace MP.GameEngine.Models.Prompts;

/// <summary>
/// Base class for a response to a <see cref="Prompt"/>. Concrete responses add
/// the payload (chosen option, dice values, played card, etc.) and a matching
/// <c>[JsonDerivedType]</c> attribute here when introduced.
/// </summary>
[JsonPolymorphic]
[JsonDerivedType(typeof(InterruptibleWindowResponse), "InterruptibleWindow")]
[JsonDerivedType(typeof(AcknowledgeResponse), "Acknowledge")]
[JsonDerivedType(typeof(DiceRollResponse), "DiceRoll")]
[JsonDerivedType(typeof(LeaveJailResponse), "LeaveJail")]
[JsonDerivedType(typeof(AcquirePropertyResponse), "AcquireProperty")]
[JsonDerivedType(typeof(TargetPlayerResponse), "TargetPlayer")]
[JsonDerivedType(typeof(TargetPropertyResponse), "TargetProperty")]
[JsonDerivedType(typeof(ShortfallResponse), "Shortfall")]
[JsonDerivedType(typeof(AuctionBidResponse), "AuctionBid")]
[JsonDerivedType(typeof(CardOptionResponse), "CardOption")]
public abstract class PromptResponse
{
    /// <summary>
    /// Must match the <see cref="Prompt.PromptId"/> of the open prompt. The
    /// provider rejects responses whose id does not match the current pending
    /// prompt.
    /// </summary>
    public string PromptId { get; init; } = "";
}
