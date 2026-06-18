using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Snapshot;
using System.Text.Json.Serialization;

namespace MP.GameEngine.Models;

public class GameCacheModel(GameDTO gameDto, GameModel game, Board board)
{
    public string ConcurrencyStamp { get; private set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// The game model with persisted game information
    /// </summary>
    private GameModel _game = game;
    private GameModel? _working;
    public GameModel Game => _working ??= new GameModel(_game);
    
    
    /// <summary>
    /// The board used in the game. Static for the game's lifetime, so it is
    /// excluded from the live state broadcast — clients fetch it once
    /// (<c>GamePlayHub.GetBoard</c>) rather than receiving it on every frame.
    /// </summary>
    [JsonIgnore]
    public Board Board { get; private set; } = board;
    
    
    /// <summary>
    /// List of event receipts that have occured since the begining of the turn.
    /// These are cleared at the start/end of each turn.
    /// </summary>
    private readonly List<EventReceipt> _events = [];

    /// <summary>
    /// Internal per-turn history (the stats source) — excluded from the live
    /// state broadcast. The live view renders from current state, not the
    /// receipt stream. See <c>design-docs/event-receipts.md</c>.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<EventReceipt> Events => _events;
    
    
    /// <summary>
    /// List of rule codes that have been executed since the begining of the turn.
    /// These are cleared at the start/end of each turn.
    /// </summary>
    private readonly List<RuleCode> _ruleCodes = [];
    public IReadOnlyList<RuleCode> RuleCodes => _ruleCodes;


    /// <summary>
    /// The open prompt the engine is currently awaiting a response for, if any.
    /// Singular by design — at most one prompt is open at any moment. In-memory
    /// only; does not survive a server restart. See <c>design-docs/choice-events.md</c>.
    /// </summary>
    public PendingPrompt? PendingPrompt { get; private set; }


    //Game Metadata
    public string GameId { get; } = gameDto.Id; //Shared on cache and game model
    public string GameName { get; } = gameDto.Name;
    public string? BoardId { get; } = gameDto.BoardId;
    public string HostPlayerId { get; } = gameDto.HostPlayerId;
    
    public GameRoundingRule RoundingRule { get; } = gameDto.RoundingRule;
        
    public GameState GameState { get; internal set; } = gameDto.State;
    
    private DiceRoll? TurnDiceRoll { get; set; }
    public TurnState TurnState { get; private set; } = TurnState.StartOfTurn;


    private void StampConcurrency()
    {
        ConcurrencyStamp = Guid.NewGuid().ToString();
    }
    
    /// <summary>
    /// Promotes the working copy to committed state (<c>_working</c> → <c>_game</c>)
    /// and re-stamps. This is the engine's single commit point and runs
    /// automatically on every turn-state change (<see cref="SetTurnState"/>).
    /// Rule services must <b>not</b> call it mid-turn: promoting detaches the
    /// model references (players, properties) that in-flight code still holds,
    /// silently dropping their later mutations. The only direct callers are the
    /// turn-boundary snapshot write-back and the one-off game bootstrap.
    /// </summary>
    public void SaveChanges()
    {
        if (_working is null) return;
        
        _game = _working;
        _working = null;
        StampConcurrency();
    }


    public DiceRoll? GetTurnDiceRoll()
    {
        if(Game.ModifiedDiceRollType is null)
            return TurnDiceRoll;
        
        return TurnDiceRoll != null 
            ? new DiceRoll(TurnDiceRoll, Game.ModifiedDiceRollType.Value) 
            : null;
    }
    

    #region Rule Codes

    internal void AddRuleCode(RuleCode ruleCode)
    {
        _ruleCodes.Add(ruleCode);
        StampConcurrency();
    }
    
    internal void ClearRuleCodes()
    {
        _ruleCodes.Clear();
        StampConcurrency();
    }

    #endregion
    
    
    #region Events

    internal void ClearEvents()
    {
        _events.Clear();
        StampConcurrency();
    }
    
    internal void AddEvent(EventReceipt eventReceipt)
    {
        // Framework-managed bookkeeping — producers set receipt-specific
        // fields (and PlayerId); cache backfills TurnNumber and the
        // per-turn sequence index. See design-docs/event-receipts.md §7.
        eventReceipt.TurnNumber = Game.Metadata.TurnNumber;
        eventReceipt.SequenceIndex = (ushort)_events.Count;

        _events.Add(eventReceipt);
        StampConcurrency();
    }

    #endregion

    #region Prompts

    internal void SetPendingPrompt(PendingPrompt pending)
    {
        if (PendingPrompt is not null)
            throw new InvalidOperationException(
                "A prompt is already pending. Resolve or clear the current prompt before opening another.");

        PendingPrompt = pending;
        StampConcurrency();
    }

    internal void ClearPendingPrompt()
    {
        if (PendingPrompt is null) return;

        PendingPrompt = null;
        StampConcurrency();
    }

    #endregion


    #region Turn Dice Roll

    internal DiceRoll? SetTurnDiceRoll(ushort die1, ushort die2, ushort thirdDie)
    {
        if(TurnState != TurnState.StartOfTurn || TurnDiceRoll is not null)
            return null;
        
        TurnDiceRoll = new DiceRoll(die1, die2, thirdDie);
        
        StampConcurrency();
        return TurnDiceRoll;
    }
    
    internal void ClearTurnDiceRoll()
    {
        TurnDiceRoll = null;
        StampConcurrency();
    }

    #endregion
    

    

    /// <summary>
    /// Sets the turn phase and commits the working copy via
    /// <see cref="SaveChanges"/>. Turn-state changes are the engine's commit
    /// boundary — the one place where promoting <c>_working</c> to <c>_game</c>
    /// is safe, because no rule code is mid-mutation holding model references.
    /// </summary>
    internal void SetTurnState(TurnState turnState)
    {
        TurnState = turnState;
        // A phase change is itself an observable state change clients must detect,
        // so always re-stamp. When there are working-copy mutations to promote,
        // SaveChanges does the stamp; otherwise stamp directly.
        if (_working is not null)
            SaveChanges();
        else
            StampConcurrency();
    }
}