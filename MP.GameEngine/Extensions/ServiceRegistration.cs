using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MP.GameEngine.Abstractions;
using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Services;
using MP.GameEngine.Services.Cards;
using MP.GameEngine.Services.Cards.Actions;
using MP.GameEngine.Services.Statistics;
using MP.GameEngine.Services.SubSystems;

namespace MP.GameEngine.Extensions;

public static class ServiceRegistration
{
    public static IServiceCollection AddGameEngine(this IServiceCollection services)
    {
        //NOTE: Framework services are not DI, they are modelled on the GameEngine class
        
        //SubSystem Services
        services.TryAddScoped<PlayerService>();
        services.TryAddScoped<DiceService>();
        services.TryAddScoped<MovementService>();
        services.TryAddScoped<BoardService>();
        services.TryAddScoped<AuctionService>();
        services.TryAddScoped<PropertyService>();
        services.TryAddScoped<PropertyCommandService>();
        services.TryAddScoped<BuildingService>();
        services.TryAddScoped<PurgingService>();
        services.TryAddScoped<GoService>();
        services.TryAddScoped<FreeParkingService>();
        services.TryAddScoped<JailService>();
        services.TryAddScoped<TaxService>();
        services.TryAddScoped<LoanService>();
        services.TryAddScoped<DealService>();
        services.TryAddScoped<GlobalEventService>();
        services.TryAddScoped<BankruptcyService>();
        
        //Main Services and Orchestrators
        services.TryAddScoped<GameEngineSetupService>();
        services.TryAddScoped<PlayerTurnOrchestrator>();
        services.TryAddScoped<TransactionService>();
        services.TryAddScoped<PropertyTransferService>();
        services.TryAddScoped<IShortfallService, ShortfallService>();
        services.TryAddScoped<StatisticsOrchestrator>();
        
        //Cards
        services.TryAddScoped<CardService>();
        services.TryAddScoped<CardTriggerService>();
        services.TryAddScoped<CardImmunityService>();
        services.TryAddScoped<ICardActionService<MoneyAction>, MoneyActionService>();
        services.TryAddScoped<ICardActionService<MovementAction>, MovementActionService>();
        services.TryAddScoped<ICardActionService<JailAction>, JailActionService>();
        services.TryAddScoped<ICardActionService<TurnsAction>, TurnsActionService>();
        services.TryAddScoped<ICardActionService<DirectionAction>, DirectionActionService>();
        services.TryAddScoped<ICardActionService<LoansAction>, LoansActionService>();
        services.TryAddScoped<ICardActionService<BuildingAction>, BuildingActionService>();
        services.TryAddScoped<ICardActionService<PropertyAction>, PropertyActionService>();
        services.TryAddScoped<ICardActionService<GlobalEventAction>, GlobalEventActionService>();
        services.TryAddScoped<ICardActionService<DeckDrawAction>, DeckDrawActionService>();
        services.TryAddScoped<ICardActionService<DiceAction>, DiceActionService>();
        services.TryAddScoped<ICardActionService<NoOpAction>, NoOpActionService>();
        services.TryAddScoped<ICardActionService<CardTransferAction>, CardTransferActionService>();

        return services;
    }
}