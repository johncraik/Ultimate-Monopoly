using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class BoardService
{
    public async Task ResolveBoardSpaceForPlayer(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var space = engine.Cache.Board.GetBoardSpace(player.BoardIndex);
        
        var propertySpace = engine.Cache.Game.GetPropertySpace(space.Index);
        if (propertySpace is not null)
        {
            switch (propertySpace.State)
            {
                case PropertyState.NotOwned:
                    //TODO call to buy/auction property
                    break;
                case PropertyState.FreeParking:
                    //TODO, pay fee into free parking (rent on FP prop goes into FP)
                    break;
                case PropertyState.Owned:
                    //TODO, pay rent to owner
                    break;
                case PropertyState.Mortgaged:
                case PropertyState.Reserved:
                    //TODO, pay nothing (ignored)
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        else
        {
            switch (space.SpaceType)
            {
                case BoardSpaceType.Tax:
                    //TODO Call tax service to get tax card, then pay tax/do what card says
                    break;
                case BoardSpaceType.Chance:
                    //TODO Call Card service to get card, and do what card says
                    break;
                case BoardSpaceType.ComChest:
                    //TODO call card service to get card, and do what card says
                    break;
                case BoardSpaceType.Go:
                    //TODO call GO service to get GO card, and collect 200/do what card says
                    break;
                case BoardSpaceType.Jail:
                    //TODO - ignore, your now in jail, end turn/progress to end turn
                    break;
                case BoardSpaceType.JustVisiting:
                    //TODO - Call just visiting service to get just visiting card, and do what card says
                    break;
                case BoardSpaceType.FreeParking:
                    //TODO - Call free parking service to get free parking card, and do what card says and/or proceed as normal
                    break;
                case BoardSpaceType.GoToJail:
                    //TODO - call jail service to get jail card, and do what card says and/or go to jail
                    break;
                case BoardSpaceType.Property:
                case BoardSpaceType.Station:
                case BoardSpaceType.Utility:
                    //Property should be in list of properties
                    throw new InvalidOperationException("Property should be in list of properties");
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
    }
}