using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums.Games;
using MP.GameEngine.Enums.Properties;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.Boards;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

public class StatisticsOrchestrator
{
    private readonly IReadOnlyList<IStatsService> _statsServices;
    
    public StatisticsOrchestrator()
    {
        _statsServices =
        [
            new CashFlowStatsService(),
            new SpendingStatsService(),
            new IncomeStatsService(),
            new PropertyStatsService(),
            new MovementStatsService(),
            new JailStatsService(),
            new FreeParkingStatsService(),
            new LoanStatsService(),
            new CardStatsService(),
            new EndgameStatsService(),
            new StateOverTimeStatsService(),
            new GraphStatsService()
        ];
    }
    
    public List<PlayerStatRecord> BuildPlayerStatRecords(CompleteGameSnapshot snapshot)
    {
        var players = snapshot.Players;
        return (from p in players 
            let record = new PlayerStatRecord(p.PlayerId) 
            //For each player in the game, creates a new player stats record
            //then calls each stats service to compute the stats, mutilating the player stats record,
            //and finally returns the list of player stats records
            select _statsServices.Aggregate(record, (current, service)
                => service.ComputeStats(current, p, snapshot)))
            .ToList();
    }

    /// <summary>
    /// A player's net worth in a given game state: cash on hand + the mortgage value of every
    /// <b>non-mortgaged</b> property they own (a mortgaged property's value is already realised
    /// as cash) + the sell-back value of all their buildings. Shared by the endgame, state-over-time
    /// and graph stats (game-stats.md §13). Building sell value uses the base (non-street-effect)
    /// rate.
    /// </summary>
    public static long CalculateNetWorth(PlayerModel player, GameModel game, Board board, GameRoundingRule roundingRule)
    {
        long netWorth = player.Money;

        foreach (var property in game.GetOwnedProperties(player.PlayerId))
        {
            if (property.State != PropertyState.Mortgaged)
                netWorth += MoneyHelper.MortgageValue(property.BoardIndex, board, roundingRule);

            netWorth += BuildingSellValue(property, board);
        }

        return netWorth;
    }

    /// <summary>
    /// The total sell-back value of the houses/hotels on a property — the number of build steps
    /// above SET (a double hotel is five house-builds beyond a hotel) times one building's sell
    /// value. Zero for properties with no buildings (incl. stations/utilities).
    /// </summary>
    private static long BuildingSellValue(PropertyModel property, Board board)
    {
        var buildSteps = property.RentLevel switch
        {
            RentLevel.ONE_HOUSE => 1,
            RentLevel.TWO_HOUSES => 2,
            RentLevel.THREE_HOUSES => 3,
            RentLevel.FOUR_HOUSES => 4,
            RentLevel.HOTEL => 5,
            RentLevel.DOUBLE_HOTEL => 10,
            _ => 0
        };

        return buildSteps == 0
            ? 0
            : (long)buildSteps * PropertySetHelper.GetSellValue(property.BoardIndex, board, hasStreetEffect: false);
    }
}