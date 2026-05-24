using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.DTOs;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Prompts;
using MP.GameEngine.Models.Snapshot;

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
    /// The board used in the game
    /// </summary>
    public Board Board { get; private set; } = board;
    
    
    /// <summary>
    /// List of event receipts that have occured since the begining of the turn.
    /// These are cleared at the start/end of each turn.
    /// </summary>
    private readonly List<EventReceipt> _events = [];
    public IReadOnlyList<EventReceipt> Events => _events;


    /// <summary>
    /// The open prompt the engine is currently awaiting a response for, if any.
    /// Singular by design — at most one prompt is open at any moment. In-memory
    /// only; does not survive a server restart. See <c>design-docs/choice-events.md</c>.
    /// </summary>
    public PendingPrompt? PendingPrompt { get; private set; }


    //Game Metadata
    public string GameId { get; } = gameDto.Id;
    public string GameName { get; } = gameDto.Name;
    public string? BoardId { get; } = gameDto.BoardId;
    public string HostPlayerId { get; } = gameDto.HostPlayerId;
    
    public GameRoundingRule RoundingRule { get; } = gameDto.RoundingRule;
        
    public GameState GameState { get; } = gameDto.State;
    public GameOutcome? GameOutcome { get; } = gameDto.Outcome;
    
    public DiceRoll? TurnDiceRoll { get; private set; }
    public TurnState TurnState { get; private set; } = TurnState.StartOfTurn;


    private void StampConcurrency()
    {
        ConcurrencyStamp = Guid.NewGuid().ToString();
    }
    
    public void SaveChanges()
    {
        if (_working is null) return;
        
        _game = _working;
        _working = null;
        StampConcurrency();
    }
    
    public void SetBoard(Board board)
    {
        Board = board;
        StampConcurrency();
    }
    
    public void ClearEvents()
    {
        _events.Clear();
        StampConcurrency();
    }
    
    public void AddEvent(EventReceipt eventReceipt)
    {
        // Framework-managed bookkeeping — producers set receipt-specific
        // fields (and PlayerId); cache backfills TurnNumber and the
        // per-turn sequence index. See design-docs/event-receipts.md §7.
        eventReceipt.TurnNumber = Game.Metadata.TurnNumber;
        eventReceipt.SequenceIndex = (ushort)_events.Count;

        _events.Add(eventReceipt);
        StampConcurrency();
    }

    public void SetPendingPrompt(PendingPrompt pending)
    {
        if (PendingPrompt is not null)
            throw new InvalidOperationException(
                "A prompt is already pending. Resolve or clear the current prompt before opening another.");

        PendingPrompt = pending;
        StampConcurrency();
    }

    public void ClearPendingPrompt()
    {
        if (PendingPrompt is null) return;

        PendingPrompt = null;
        StampConcurrency();
    }

    public void SetTurnDiceRoll(ushort die1, ushort die2, ushort thirdDie)
    {
        if(TurnState != TurnState.StartOfTurn || TurnDiceRoll is not null)
            return;
        
        TurnDiceRoll = new DiceRoll(die1, die2, thirdDie);
        
        //Internally stamps concurrency
        SetTurnState(TurnState.PlayerRollMovement);
    }
    
    internal void SetTurnState(TurnState turnState)
    {
        TurnState = turnState;
        StampConcurrency();
    }
}