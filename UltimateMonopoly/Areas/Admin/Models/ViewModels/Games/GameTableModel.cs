using JC.Core.Models.Pagination;
using MP.GameEngine.Enums.Games;

namespace UltimateMonopoly.Areas.Admin.Models.ViewModels.Games;

public class GameTableModel
{
    public PagedList<GameViewModel> Games { get; }
    public string? Search { get; }
    public string? HostId { get; }
    public GameState? State { get; }
    public GameOutcome? Outcome { get; }

    /// <summary>Preview mode (the §7.3 Recent Activity panel): the partial drops its pagination + count header.</summary>
    public bool Preview { get; init; }

    public GameTableModel(PagedList<GameViewModel> games, string? search, string? hostId,
        GameState? state, GameOutcome? outcome)
    {
        Games = games;
        Search = search;
        HostId = hostId;
        State = state;
        Outcome = outcome;
    }
}