using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;
using UltimateMonopoly.Models.ViewModels.Social;

namespace UltimateMonopoly.Models.ViewModels.Games;

/// <summary>
/// Model for <c>_BoardView</c>. <see cref="Board"/> supplies the static layout
/// (spaces, sets, indexes); <see cref="Properties"/> supplies live ownership
/// state (owner / mortgaged / reserved / free-parking) keyed by board index.
/// </summary>
public record BoardViewModel(Board Board, List<PropertyModel> Properties, List<(UserProfileViewModel Profile, PlayerModel Model)> Players);