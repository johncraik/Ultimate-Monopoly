using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.Cards;

public class CardImmunityService
{
    private readonly ICardCacheService _cacheService;

    public CardImmunityService(ICardCacheService cacheService)
    {
        _cacheService = cacheService;
    }
    
    public async Task<bool> CheckSwappingMoneyImmunity(Framework.GameEngine engine, PlayerModel subject,
        CancellationToken ct)
        => await CheckImmunity(engine, subject, CardImmunity.SwappingMoney, ct);

    public async Task<bool> CheckGoToJailCardImmunity(Framework.GameEngine engine, PlayerModel subject,
        CancellationToken ct)
        => await CheckImmunity(engine, subject, CardImmunity.GoToJailCard, ct);
    
    public async Task<bool> CheckCancelledTripleBonusImmunity(Framework.GameEngine engine, PlayerModel subject,
        CancellationToken ct)
        => await CheckImmunity(engine, subject, CardImmunity.CancelledTripleBonus, ct);

    public async Task<bool> CheckPurgingPropertyImmunity(Framework.GameEngine engine, PlayerModel subject,
        CancellationToken ct)
        => await CheckImmunity(engine, subject, CardImmunity.PurgedProperty, ct);
    
    public async Task<bool> CheckReturningPropertyImmunity(Framework.GameEngine engine, PlayerModel subject,
        CancellationToken ct)
        => await CheckImmunity(engine, subject, CardImmunity.ReturningProperty, ct);


    private async Task<bool> CheckImmunity(Framework.GameEngine engine, PlayerModel subject, CardImmunity immunity, CancellationToken ct)
    {
        var immunityCard = (await subject.GetCards(_cacheService))
            .FirstOrDefault(c => 
                c.Groups.SelectMany(g => g.Actions)
                    .OfType<ImmunityAction>()
                    .Any(a => a.Immunity == immunity));
        if(immunityCard == null) return false;

        var response = await engine.PromptProvider.RequestAsync(new CardOptionPrompt
        {
            PlayerId = subject.PlayerId,
            Title = "Play Immunity Card?",
            Body = "Would you like to play your immunity card?",
            Options =
                [new CardOption(immunityCard.CardId, immunityCard.GetDisplayText(engine.Cache, subject.PlayerId))],
            PlayCardChoice = true
        }, ct: ct);
        
        //Only the offered immunity card's own id counts as "play" — an empty (decline) or any other key
        //is a no-play (M-01: previously any non-empty key played the immunity card without matching the id).
        if(response.SelectedKey != immunityCard.CardId)
            return false;

        _ = await engine.CardService.PlayCard(engine, subject, immunityCard, ct);
        return true;
    }
}