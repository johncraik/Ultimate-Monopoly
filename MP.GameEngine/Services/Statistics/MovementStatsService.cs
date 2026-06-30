using MP.GameEngine.Abstractions;
using MP.GameEngine.Enums;
using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;
using MP.GameEngine.Models.EventReceipts;
using MP.GameEngine.Models.Snapshot;
using MP.GameEngine.Models.Statistics;

namespace MP.GameEngine.Services.Statistics;

public class MovementStatsService : IStatsService
{
    public PlayerStatRecord ComputeStats(PlayerStatRecord record, PlayerModel player, CompleteGameSnapshot snapshot)
    {
        uint turnRolls = 0;
        uint cardRolls = 0;
        uint doublesRolled = 0;
        uint triplesRolled = 0;

        uint someoneRolledNumber = 0;
        uint youRolledNumber = 0;

        uint changedDirection = 0;
        long distanceClockwise = 0;
        long distanceAntiClockwise = 0;

        var landOnIndexes = new Dictionary<ushort, uint>();

        foreach (var turn in snapshot.Turns)
        {
            var diceRolls = turn.Events
                .OfType<DiceRollReceipt>()
                .ToList();

            //Dice rolls:
            var playerRolls = diceRolls
                .Where(dr => dr.PlayerId == player.PlayerId)
                .ToList();

            turnRolls += (uint)playerRolls.Count(dr => dr.IsTurnRoll);
            //Card rolls (dice-off / multiplier card rolls) are the non-turn rolls — 1-2 dice, no third die.
            cardRolls += (uint)playerRolls.Count(dr => !dr.IsTurnRoll);

            //Doubles and triples (turn rolls only carry these types; card rolls are always Normal):
            doublesRolled += (uint)playerRolls.Count(dr => dr.RollType == DiceRollType.Double);
            triplesRolled += (uint)playerRolls.Count(dr => dr.RollType == DiceRollType.Triple);

            //Your number — TURN rolls only (a card roll's two dice could otherwise false-match a player's
            //number) and never a triple (a triple is its own mechanic, not a dice-number hit — mirrors
            //PlayerModel.IsDiceNumber).
            someoneRolledNumber += (uint)diceRolls.Count(dr => dr.PlayerId != player.PlayerId
                                                               && dr.IsTurnRoll && dr.RollType != DiceRollType.Triple
                                                               && ((dr.Die1 == player.Dice1 && dr.Die2 == player.Dice2)
                                                                   || (dr.Die1 == player.Dice2 && dr.Die2 == player.Dice1)));
            youRolledNumber += (uint)playerRolls.Count(dr => dr.IsTurnRoll && dr.RollType != DiceRollType.Triple
                                                             && ((dr.Die1 == player.Dice1 && dr.Die2 == player.Dice2)
                                                                 || (dr.Die1 == player.Dice2 && dr.Die2 == player.Dice1)));

            //Movements:

            var playerAtStartTurn = turn.Game.GetPlayer(player.PlayerId);
            if (playerAtStartTurn is null)
                //Null when bankrupt, so player wont be moving anyway
                continue;

            var direction = playerAtStartTurn.Direction;
            foreach (var e in turn.Events.Where(p => p.PlayerId == player.PlayerId))
            {
                if (e is not PlayerMovedReceipt && e is not PlayerDirectionChangedReceipt)
                    continue;

                if (e is PlayerDirectionChangedReceipt)
                {
                    //Flip the direction (for counting movement)
                    direction = direction switch
                    {
                        PlayerDirection.Forward => PlayerDirection.Backward,
                        PlayerDirection.Backward => PlayerDirection.Forward,
                        _ => throw new ArgumentOutOfRangeException()
                    };
                    changedDirection++;
                    continue;
                }

                var movementEvent = e as PlayerMovedReceipt;
                if (movementEvent == null) continue;

                if (movementEvent.InitialBoardIndex == IndexHelper.JailSpace
                    || movementEvent.FinalBoardIndex == IndexHelper.JailSpace)
                    //Dont count jail (to or leave) as movement
                    //Leaving jail moves player to just visiting by default
                    continue;

                //Land on counts:
                var res = landOnIndexes.TryGetValue(movementEvent.FinalBoardIndex, out var count);
                if(!res)
                    landOnIndexes.Add(movementEvent.FinalBoardIndex, 1);
                else
                    landOnIndexes[movementEvent.FinalBoardIndex] = count + 1;

                //Distance moved this step = the shorter arc between initial and final (every
                //move is fewer than half the board, so the shorter arc IS the path travelled).
                var clockwiseArc = (movementEvent.FinalBoardIndex - movementEvent.InitialBoardIndex + IndexHelper.PhysicalBoardSize) % IndexHelper.PhysicalBoardSize;
                var antiClockwiseArc = (movementEvent.InitialBoardIndex - movementEvent.FinalBoardIndex + IndexHelper.PhysicalBoardSize) % IndexHelper.PhysicalBoardSize;
                var steps = Math.Min(clockwiseArc, antiClockwiseArc);

                //Signed by travel direction: moving counter to the player's direction of travel
                //comes OFF the count (a forward-3/back-3 nets to 0). Bucketed by the player's
                //facing — clockwise vs anti-clockwise is their travel orientation (PlayerDirection).
                var signedSteps = movementEvent.Direction == PlayerMovementDirection.DirectionOfTravel ? steps : -steps;
                if (direction == PlayerDirection.Forward)
                    distanceClockwise += signedSteps;
                else
                    distanceAntiClockwise += signedSteps;
            }
        }

        //Set the stats:
        record.TotalTurnRolls = turnRolls;
        record.TotalCardRolls = cardRolls;
        record.DoublesRolled = doublesRolled;
        record.TriplesRolled = triplesRolled;

        record.TimesSomeoneRolledYourDiceNumber = someoneRolledNumber;
        record.TimesYouRolledYourDiceNumber = youRolledNumber;

        record.TimesChangedDirection = changedDirection;
        record.TotalDistanceTraveledClockwise = distanceClockwise;
        record.TotalDistanceTraveledCounterClockwise = distanceAntiClockwise;
        
        //Guard the empty case: MaxBy on an empty sequence throws for a value-type element (KeyValuePair),
        //so a player with no counted landings (finished / cancel-edge / drawn / early-bankrupt / no-move)
        //would otherwise throw and sink the WHOLE game's stats projection (H-03). Default to GO (index 0)
        //with a 0 count, so the record stays internally consistent.
        if (landOnIndexes.Count > 0)
        {
            var mostLanded = landOnIndexes.MaxBy(kv => kv.Value);
            record.MostLandedOnBoardIndex = mostLanded.Key;
            record.MostLandedOnBoardIndexCount = mostLanded.Value;
        }
        else
        {
            record.MostLandedOnBoardIndex = IndexHelper.GoSpace;
            record.MostLandedOnBoardIndexCount = 0;
        }
        record.TimesLandedOnGo = landOnIndexes.TryGetValue(IndexHelper.GoSpace, out var goCount) ? goCount : 0;
        record.TimesLandedOnFreeParking = landOnIndexes.TryGetValue(IndexHelper.FreeParkingSpace, out var fpCount) ? fpCount : 0;
        
        var incomeTaxCount = landOnIndexes.TryGetValue(IndexHelper.IncomeTaxSpace, out var incomeTaxCountValue) ? incomeTaxCountValue : 0;
        var superTaxCount = landOnIndexes.TryGetValue(IndexHelper.SuperTaxSpace, out var superTaxCountValue) ? superTaxCountValue : 0;
        record.TimesLandedOnTax = incomeTaxCount + superTaxCount;

        return record;
    }
}