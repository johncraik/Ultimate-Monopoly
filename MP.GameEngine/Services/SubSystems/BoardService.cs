using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Snapshot;

namespace MP.GameEngine.Services.SubSystems;

public class BoardService
{
    private readonly GoService _goService;
    private readonly JailService _jailService;
    private readonly FreeParkingService _fpService;
    private readonly PropertyService _propertyService;
    private readonly TaxService _taxService;

    public BoardService(GoService goService, 
        JailService jailService,
        FreeParkingService fpService,
        PropertyService propertyService,
        TaxService taxService)
    {
        _goService = goService;
        _jailService = jailService;
        _fpService = fpService;
        _propertyService = propertyService;
        _taxService = taxService;
    }

    public async Task ResolveBoardSpaceForPlayer(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var space = engine.Cache.Board.GetBoardSpace(player.BoardIndex);

        var propertySpace = engine.Cache.Game.GetPropertySpace(space.Index);
        if (propertySpace is not null)
        {
            switch (propertySpace.State)
            {
                case PropertyState.NotOwned:
                    if (player.HasPassedInitialGo)
                        await _propertyService.ProcessUnownedProperty(engine, player, space, propertySpace, ct);
                    else
                        //Cite rule that u must pass go to buy:
                        engine.CiteRule(RuleCode.Default_BuyRequiresPassingGo);
                    break;
                case PropertyState.FreeParking:
                    //Only needs the board space info (rents), "single" rent always assumed for FP property
                    //propertySpace is not needed as state already validated as being in FP
                    await _fpService.PayPropertyFee(engine, player, space, ct);
                    break;
                case PropertyState.Owned:
                    await _propertyService.PayPropertyRent(engine, player, space, propertySpace, ct);
                    break;
                case PropertyState.Mortgaged:
                case PropertyState.Reserved:
                default:
                    //nothing happens
                    break;
            }
        }
        else
        {
            switch (space.SpaceType)
            {
                case BoardSpaceType.Tax:
                    await _taxService.PayTax(engine, player, ct);
                    break;
                case BoardSpaceType.Chance:
                    //TODO Call Card service to get card, and do what card says
                    break;
                case BoardSpaceType.ComChest:
                    //TODO call card service to get card, and do what card says
                    break;
                case BoardSpaceType.Go:
                    await _goService.LandOnGo(engine, player, ct);
                    break;
                case BoardSpaceType.JustVisiting:
                    await HandleJustVisiting(engine, player, ct);
                    break;
                case BoardSpaceType.FreeParking:
                    await _fpService.ProcessFreeParking(engine, player, ct);
                    break;
                case BoardSpaceType.GoToJail:
                    await _jailService.GoToJail(engine, player, ct);
                    break;
                case BoardSpaceType.Property:
                case BoardSpaceType.Station:
                case BoardSpaceType.Utility:
                    //Property should be in list of properties
                    throw new InvalidOperationException("Property should be in list of properties");
                case BoardSpaceType.Jail:
                default:
                    //nothing, since jail is no-op
                    break;
            }
        }
    }


    private async Task HandleJustVisiting(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        //TODO get a just visiting card:
        //Then do what the card says
    }
}