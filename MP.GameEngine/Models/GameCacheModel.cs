using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Models;

public class GameCacheModel(GameModel game, Board board)
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


    public void StampConcurrency()
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
        _events.Add(eventReceipt);
        StampConcurrency();
    }
}