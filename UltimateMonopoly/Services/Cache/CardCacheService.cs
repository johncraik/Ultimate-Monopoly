using Microsoft.Extensions.Caching.Memory;
using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Models.Snapshot.Cards;
using UltimateMonopoly.Services.Imports;

namespace UltimateMonopoly.Services.Cache;

public class CardCacheService : ICardCacheService
{
    private readonly IMemoryCache _memoryCache;
    private readonly CardImportService _cardImportService;
    private const string CacheKey = "Cards";
    private const string AllCardsKey = "<ALL>";
    private static readonly TimeSpan CardExpiration = TimeSpan.FromHours(12);

    public CardCacheService(IMemoryCache memoryCache,
        CardImportService cardImportService)
    {
        _memoryCache = memoryCache;
        _cardImportService = cardImportService;
    }
    
    private string GetKey(string cardId)
        => $"{CacheKey}__{cardId}";
    
    public async Task<List<CardModel>> GetCards()
        => await _memoryCache.GetOrCreateAsync(GetKey(AllCardsKey), async entry =>
        {
            entry.Priority = CacheItemPriority.NeverRemove;
            return await _cardImportService.ImportCards();
        }) ?? throw new InvalidOperationException("Failed to get all cards");
    
    public async Task<CardModel?> GetCard(string cardId)
        => await _memoryCache.GetOrCreateAsync(GetKey(cardId), async entry =>
        {
            entry.SlidingExpiration = CardExpiration;
            var cards = await GetCards();
            return cards.FirstOrDefault(c => c.CardId == cardId);
        });
}