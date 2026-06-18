using MP.GameEngine.Enums.Cards;
using MP.GameEngine.Models.Cards.Actions;
using MP.GameEngine.Models.Prompts.PromptTypes;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Services.Cards;

public class CardImmunityService
{
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
        var immunityCard = subject.Cards.FirstOrDefault(c =>
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
        
        if(string.IsNullOrEmpty(response.SelectedKey))
            return false;
        
        _ = await engine.CardService.PlayCard(engine, subject, immunityCard, ct);
        return true;
    }
}