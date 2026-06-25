using MP.GameEngine.Models.Snapshot.Cards;

namespace MP.GameEngine.Abstractions.Cards;

public interface ICardCacheService
{
    Task<List<CardModel>> GetCards();

    Task<CardModel?> GetCard(string cardId);
}