using JC.Core.Enums;
using JC.Core.Extensions;
using JC.Core.Models;
using JC.Core.Services.DataRepositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MP.GameEngine.Abstractions.Cards;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Deals;
using MP.GameEngine.Services.SubSystems;
using UltimateMonopoly.Models.DataModels.Games;
using UltimateMonopoly.Services.GameEngine;
using EngineRuntime = MP.GameEngine.Services.Framework.GameEngine;

namespace UltimateMonopoly.Services.Games;

public class PlayerProfileService
{
    private readonly IRepositoryManager _repos;
    private readonly IUserInfo _userInfo;
    private readonly UserService _userService;
    private readonly IGameExecutor _executor;
    private readonly ICardCacheService _cacheService;

    public PlayerProfileService(IRepositoryManager repos,
        IUserInfo userInfo,
        UserService userService,
        IGameExecutor executor,
        ICardCacheService cacheService)
    {
        _repos = repos;
        _userInfo = userInfo;
        _userService = userService;
        _executor = executor;
        _cacheService = cacheService;
    }


    private IQueryable<GamePlayer> QueryGamePlayers(string gameId, GameState state, bool includeGame, bool includeBoardSkin)
    {
        var query = _repos.GetRepository<GamePlayer>()
            .AsQueryable().FilterDeleted(DeletedQueryType.OnlyActive);

        switch (includeGame)
        {
            case true when includeBoardSkin:
                query = query.Include(p => p.Game)
                    .ThenInclude(g => g.BoardSkin);
                break;
            case true:
                query = query.Include(p => p.Game);
                break;
        }
        
        return query.Where(p => p.GameId == gameId && p.Game.State == state);
    }

    public async Task<GamePlayer?> GetPlayerForGameSetup(string gameId, string userId)
        => await QueryGamePlayers(gameId, GameState.Setup, true, false)
            .FirstOrDefaultAsync(p => p.UserId == userId);

    public async Task<GamePlayer?> GetPlayerForGamePlay(string gameId, string userId)
        => await QueryGamePlayers(gameId, GameState.InPlay, true, false)
            .FirstOrDefaultAsync(p => p.UserId == userId);


    // ─── Portfolio commands ──────────────────────────────────────────────
    // Player-initiated property actions. Each enqueues onto the game's
    // single-writer executor; the engine command (PropertyService.Try…)
    // self-validates and opens its AcquirePropertyPrompt confirmation, parking
    // the work item until the player answers. The acting player is always the
    // current player (portfolio commands are current-player-only); host-bypass
    // lets the host drive it on a player's behalf. See web-orchestration.md §3.

    public void EnqueueMortgage(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.MortgageProperty(engine, boardIndex, ct));

    public void EnqueueUnmortgage(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.UnmortgageProperty(engine, boardIndex, ct));

    public void EnqueueUnReserve(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.UnReserveProperty(engine, boardIndex, ct));

    public void EnqueueBuild(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.BuildOnProperty(engine, boardIndex, ct));

    public void EnqueueBuildSet(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId, (svc, engine, ct) =>
        {
            // The button carries any index in the set; resolve it to the set. A
            // non-property index (shouldn't happen) is a no-op.
            var set = PropertySetHelper.ResolveSet(boardIndex);
            return set is null ? Task.CompletedTask : svc.BuildOnProperties(engine, set.Value, ct);
        });

    public void EnqueueBuildAll(string gameId, string submittingUserId)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.BuildOnAllProperties(engine, ct));

    public void EnqueueSell(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.SellOnProperty(engine, boardIndex, ct));

    public void EnqueueSellSet(string gameId, string submittingUserId, ushort boardIndex)
        => EnqueuePortfolioCommand(gameId, submittingUserId, (svc, engine, ct) =>
        {
            var set = PropertySetHelper.ResolveSet(boardIndex);
            return set is null ? Task.CompletedTask : svc.SellOnProperties(engine, set.Value, ct);
        });

    public void EnqueueSellAll(string gameId, string submittingUserId)
        => EnqueuePortfolioCommand(gameId, submittingUserId,
            (svc, engine, ct) => svc.SellOnAllProperties(engine, ct));

    // Custom loan repayment — current-player portfolio command, but it resolves
    // LoanService (not PropertyService), so it has its own enqueue with the same
    // writer-thread gate re-check. RepayLoansCustom resolves the current player itself.
    public void EnqueueRepayLoanCustom(string gameId, string submittingUserId, uint amount)
        => _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanPortfolioCommand(current.PlayerId, submittingUserId))
                return;

            await sp.GetRequiredService<LoanService>().RepayLoansCustom(engine, amount, ct);
        });

    // Leave jail by paying the fee — current-player jail-exit command (CanLeaveJail,
    // host-bypass aware). Resolves JailService (not PropertyService), so it has its own
    // enqueue with the writer-thread gate re-check. LeaveJailByPaying pays the fee,
    // moves the player to Just Visiting, and escalates the next jail cost.
    public void EnqueueLeaveJailPay(string gameId, string submittingUserId)
        => _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanLeaveJail(current.PlayerId, submittingUserId))
                return;

            await sp.GetRequiredService<JailService>().LeaveJailByPaying(engine, current, ct);
        });

    // Leave jail by playing a specific held Get Out of Jail Free card — the card sibling of
    // EnqueueLeaveJailPay (same CanLeaveJail gate, host-bypass aware). The player may hold more than
    // one such card (one per standard deck), so the chosen card is named by id; CardService.PlayCard
    // resolves it (its Release action moves the player to Just Visiting) then consumes it. No-ops if
    // the named card isn't in hand.
    public void EnqueueLeaveJailCard(string gameId, string submittingUserId, string cardId)
        => _executor.Enqueue(gameId, async (engine, _, ct) =>
        {
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanLeaveJail(current.PlayerId, submittingUserId))
                return;

            var playerCards = await current.GetCards(_cacheService);
            var card = playerCards.FirstOrDefault(c => c.CardId == cardId);
            if (card is null)
                return;

            await engine.CardService.PlayCard(engine, current, card, ct);
        });

    // Play a held card from hand at the start of the player's own turn — the OnTurnStart play window
    // (the engine fires OnSpaceLand itself on landing; this is the explicit command path). A portfolio
    // command (CanPortfolioCommand: StartOfTurn, current player, not jailed, idle — its doc lists
    // "play card from hand"). Resolves the named card in hand by id and plays it via
    // CardService.PlayCard (resolve → consume, or retain if it has TurnsRemaining). No-ops if not held.
    public void EnqueuePlayCard(string gameId, string submittingUserId, string cardId)
        => _executor.Enqueue(gameId, async (engine, _, ct) =>
        {
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanPortfolioCommand(current.PlayerId, submittingUserId))
                return;
            
            var playerCards = await current.GetCards(_cacheService);
            var card = playerCards.FirstOrDefault(c => c.CardId == cardId);
            if (card is null)
                return;

            await engine.CardService.PlayCard(engine, current, card, ct);
        });

    // Turn-boundary deal command. Unlike portfolio commands the proposer isn't necessarily the
    // current player (deals fire at any turn boundary), so the proposer id is explicit and the
    // gate is CanDeal (host-bypass aware). DealService resolves the players and runs the deal.
    public void EnqueueProposeDeal(string gameId, string submittingUserId, string proposerId,
        string counterPartyId, DealContents contents)
        => _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread (web-orchestration.md §5).
            if (!engine.TurnStateProvider.CanDeal(proposerId, submittingUserId))
                return;

            await sp.GetRequiredService<DealService>()
                .ProposeDealCommand(engine, proposerId, counterPartyId, contents, ct);
        });

    // Voluntary bankruptcy. Like a deal the target player is explicit (bankruptcy isn't
    // a current-player-only command — any player can quit at a turn boundary, and the
    // host can declare on their behalf), gated by CanDeclareBankruptcy. BankruptcyService
    // returns the player's assets to the bank and, if they were the last solvent player,
    // concludes the game.
    public void EnqueueDeclareBankruptcy(string gameId, string submittingUserId, string playerId)
        => _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread (web-orchestration.md §5).
            if (!engine.TurnStateProvider.CanDeclareBankruptcy(playerId, submittingUserId))
                return;

            await sp.GetRequiredService<BankruptcyService>()
                .DeclareBankruptcy(engine, playerId, ct);
        });

    private void EnqueuePortfolioCommand(string gameId, string submittingUserId,
        Func<PropertyCommandService, EngineRuntime, CancellationToken, Task> command)
    {
        _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread (web-orchestration.md §5):
            // the hub's pre-check can go stale before this item runs, so a no-longer-valid
            // command no-ops here rather than running into the engine. Portfolio commands
            // act on the current player; CanPortfolioCommand is host-bypass aware.
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanPortfolioCommand(current.PlayerId, submittingUserId))
                return;

            var properties = sp.GetRequiredService<PropertyCommandService>();
            await command(properties, engine, ct);
        });
    }
    
    private void EnqueuePortfolioCommand(string gameId, string submittingUserId,
        Func<BuildingService, EngineRuntime, CancellationToken, Task> command)
    {
        _executor.Enqueue(gameId, async (engine, sp, ct) =>
        {
            // Authoritative gate re-check on the writer thread (web-orchestration.md §5):
            // the hub's pre-check can go stale before this item runs, so a no-longer-valid
            // command no-ops here rather than running into the engine. Portfolio commands
            // act on the current player; CanPortfolioCommand is host-bypass aware.
            var current = engine.Cache.Game.CurrentPlayer();
            if (current is null || !engine.TurnStateProvider.CanPortfolioCommand(current.PlayerId, submittingUserId))
                return;

            var properties = sp.GetRequiredService<BuildingService>();
            await command(properties, engine, ct);
        });
    }
}