using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Services.Cards;

namespace MP.GameEngine.Services.SubSystems;

public class BoardService
{
    private readonly GoService _goService;
    private readonly JailService _jailService;
    private readonly FreeParkingService _fpService;
    private readonly PropertyService _propertyService;
    private readonly TaxService _taxService;
    private readonly CardTriggerService _triggerService;

    public BoardService(GoService goService, 
        JailService jailService,
        FreeParkingService fpService,
        PropertyService propertyService,
        TaxService taxService,
        CardTriggerService triggerService)
    {
        _goService = goService;
        _jailService = jailService;
        _fpService = fpService;
        _propertyService = propertyService;
        _taxService = taxService;
        _triggerService = triggerService;
    }

    public async Task ResolveBoardSpaceForPlayer(Framework.GameEngine engine, PlayerModel player, CancellationToken ct)
    {
        var suppressDefault = await _triggerService.OnSpaceLand(engine, player, ct);
        if (suppressDefault.SuppressBoardResolution) return;
        
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
                    await engine.CardService.DrawCard(engine, player, CardType.Chance, ct);
                    if (player.Direction == PlayerDirection.Backward)
                    {
                        engine.CiteRule(RuleCode.Percentage_Card);
                        engine.CiteRule(RuleCode.Percentage_Card_Cap);
                        await engine.CardService.DrawCard(engine, player, CardType.PercentageChance, ct);
                        
                        engine.CiteRule(RuleCode.Third_Card_AntiClockwise);
                        await engine.CardService.DrawCard(engine, player, CardType.Third, ct);
                    }
                    break;
                case BoardSpaceType.ComChest:
                    await engine.CardService.DrawCard(engine, player, CardType.CommunityChest, ct);
                    if (player.Direction == PlayerDirection.Backward)
                    {
                        engine.CiteRule(RuleCode.Percentage_Card);
                        engine.CiteRule(RuleCode.Percentage_Card_Cap);
                        await engine.CardService.DrawCard(engine, player, CardType.PercentageComChest, ct);
                        
                        engine.CiteRule(RuleCode.Third_Card_AntiClockwise);
                        await engine.CardService.DrawCard(engine, player, CardType.Third, ct);
                    }
                    break;
                case BoardSpaceType.Go:
                    await _goService.LandOnGo(engine, player, ct);
                    break;
                case BoardSpaceType.JustVisiting:
                    //First check if player is prevented from drawing a just visiting card:
                    if(!engine.Cache.PreventBoardIndexCard(player.PlayerId, space.Index))
                        //All just visiting does is draw a card (then its a no-op space)
                        await engine.CardService.DrawCard(engine, player, CardType.JustVisiting, ct);
                    
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
}