using MP.GameEngine.Helpers.RuleSet;

namespace MP.GameEngine.Helpers;

public static class DoubleEffects
{
    public record DoubleEffect(
        string Title,
        string Body,
        ushort DoubleValue,
        IReadOnlyList<int> RollerSteps,
        IReadOnlyList<int> OtherPlayerSteps,
        bool RollerMissesTurn = false,
        bool OtherPlayerMissesTurn = false,
        ushort? SnakeEyesBonus = null);

    public static DoubleEffect For(ushort diceValue)
        => diceValue switch
        {
            1 => new DoubleEffect(DoubleDiceDictionary.DoubleOneTitle,
                DoubleDiceDictionary.DoubleOneBody, 1,
                [DoubleDiceDictionary.DoubleOneMovement], [],
                SnakeEyesBonus: RuleDictionary.SnakeEyesBonus),
            2 => new DoubleEffect(DoubleDiceDictionary.DoubleTwoTitle,
                DoubleDiceDictionary.DoubleTwoBody, 2,
                [DoubleDiceDictionary.DoubleTwoMovement], [],
                OtherPlayerMissesTurn: true),
            3 => new DoubleEffect(DoubleDiceDictionary.DoubleThreeTitle,
                DoubleDiceDictionary.DoubleThreeBody, 3,
                [DoubleDiceDictionary.DoubleThreeForwardMovement, DoubleDiceDictionary.DoubleThreeBackwardMovement], []),
            4 => new DoubleEffect(DoubleDiceDictionary.DoubleFourTitle,
                DoubleDiceDictionary.DoubleFourBody, 4,
                [], [DoubleDiceDictionary.DoubleFourForwardMovement, DoubleDiceDictionary.DoubleFourBackwardMovement]),
            5 => new DoubleEffect(DoubleDiceDictionary.DoubleFiveTitle,
                DoubleDiceDictionary.DoubleFiveBody, 5,
                [DoubleDiceDictionary.DoubleFiveForwardMovement], [DoubleDiceDictionary.DoubleFiveBackwardMovement],
                RollerMissesTurn: true),
            6 => new DoubleEffect(DoubleDiceDictionary.DoubleSixTitle,
                DoubleDiceDictionary.DoubleSixBody, 6,
                [DoubleDiceDictionary.DoubleSixMovement], []),
            _ => throw new ArgumentOutOfRangeException(nameof(diceValue), diceValue, null)
        };
}