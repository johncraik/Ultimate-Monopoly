namespace MP.GameEngine.Helpers.RuleSet;

public class DoubleDiceDictionary
{
    public const string DoubleOneTitle = "SNAKE EYES";
    public const string DoubleOneBody = "You rolled a double one! Collect £500.";
    public const int DoubleOneMovement = 2;
    
    public const string DoubleTwoTitle = "DOUBLE TWO";
    public const string DoubleTwoBody = "You rolled a double two! Every other player will miss a go!";
    public const int DoubleTwoMovement = 4;

    public const string DoubleThreeTitle = "DOUBLE THREE";
    public const string DoubleThreeBody = "You rolled a double three! You will move forward 3 spaces, then back 3 spaces.";
    public const int DoubleThreeForwardMovement = 3;
    public const int DoubleThreeBackwardMovement = -3;
    
    public const string DoubleFourTitle = "DOUBLE FOUR";
    public const string DoubleFourBody = "You rolled a double four! All other players will move forward 4 spaces, then back 4 spaces.";
    public const int DoubleFourForwardMovement = 4;
    public const int DoubleFourBackwardMovement = -4;
    
    public const string DoubleFiveTitle = "DOUBLE FIVE";
    public const string DoubleFiveBody = "You rolled a double five! You will move forward 10 spaces, and miss a turn. All other players will move back 10 spaces.";
    public const int DoubleFiveForwardMovement = 10;
    public const int DoubleFiveBackwardMovement = -10;
    
    public const string DoubleSixTitle = "DOUBLE SIX";
    public const string DoubleSixBody = "You rolled a double six! You will move back 12 spaces.";
    public const int DoubleSixMovement = -12;
}