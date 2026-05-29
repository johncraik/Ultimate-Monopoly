using MP.GameEngine.Enums.Players;
using MP.GameEngine.Helpers;

// ReSharper disable InconsistentNaming
namespace MP.GameEngine.Tests.HelperTests;

/// <summary>
/// Scenario coverage for <see cref="IndexHelper.MoveIndex"/> and
/// <see cref="IndexHelper.AdvanceIndex"/> — the board-movement maths.
///
/// A GO pass = the move's journey involves GO without ending on it. So
/// crossing over GO (35 → 42, ending on 2) is a pass, and departing GO
/// (sitting on GO, then rolling off — "I'm on GO, I rolled, I've passed GO")
/// is a pass — but landing exactly on GO (35 → 40) is NOT, that's a landing.
/// Direction-symmetric.
///
/// Every failing row below is Forward-only: forward movement counts a landing
/// on GO as a pass (it shouldn't) and fails to count departing GO as a pass
/// (it should). Backward movement is correct.
/// </summary>
public class IndexHelper_Tests
{
    // ─── MoveIndex — no GO involved ─────────────────────────────────────
    // Plain offset within the board, no wrap, zero passes.

    [Theory]
    // Forward
    [InlineData(5, 7, PlayerDirection.Forward, 12, 0)]
    [InlineData(10, 9, PlayerDirection.Forward, 19, 0)]
    [InlineData(20, 12, PlayerDirection.Forward, 32, 0)]
    [InlineData(27, 12, PlayerDirection.Forward, 39, 0)]   // lands on 39, just shy of GO
    [InlineData(30, 9, PlayerDirection.Forward, 39, 0)]
    [InlineData(10, 0, PlayerDirection.Forward, 10, 0)]    // no move
    // Backward
    [InlineData(20, 12, PlayerDirection.Backward, 8, 0)]
    [InlineData(39, 12, PlayerDirection.Backward, 27, 0)]
    [InlineData(13, 12, PlayerDirection.Backward, 1, 0)]   // lands on 1, just shy of GO
    [InlineData(30, 18, PlayerDirection.Backward, 12, 0)]
    [InlineData(35, 5, PlayerDirection.Backward, 30, 0)]
    [InlineData(10, 0, PlayerDirection.Backward, 10, 0)]   // no move
    public void MoveIndex_NoGoCrossing_ReturnsOffsetWithZeroPasses(
        int index, ushort spaces, PlayerDirection direction, int expectedIndex, int expectedPasses)
    {
        var result = IndexHelper.MoveIndex((ushort)index, spaces, direction);
        Assert.Equal((ushort)expectedIndex, result.Index);
        Assert.Equal((ushort)expectedPasses, result.GoPasses);
    }


    // ─── MoveIndex — travels past GO ────────────────────────────────────
    // Wraps over the GO boundary and lands beyond it: one pass.

    [Theory]
    // Forward (clockwise over 39 → 0 → …)
    [InlineData(29, 12, PlayerDirection.Forward, 1, 1)]
    [InlineData(35, 12, PlayerDirection.Forward, 7, 1)]
    [InlineData(39, 12, PlayerDirection.Forward, 11, 1)]
    [InlineData(30, 18, PlayerDirection.Forward, 8, 1)]
    [InlineData(38, 5, PlayerDirection.Forward, 3, 1)]
    [InlineData(35, 7, PlayerDirection.Forward, 2, 1)]     // 35 → 42 (2): crosses past GO
    // Backward (anti-clockwise over 0 → 39 → …)
    [InlineData(5, 12, PlayerDirection.Backward, 33, 1)]
    [InlineData(11, 12, PlayerDirection.Backward, 39, 1)]
    [InlineData(1, 12, PlayerDirection.Backward, 29, 1)]
    [InlineData(8, 12, PlayerDirection.Backward, 36, 1)]
    [InlineData(6, 18, PlayerDirection.Backward, 28, 1)]
    public void MoveIndex_PassesGo_CountsOnePass(
        int index, ushort spaces, PlayerDirection direction, int expectedIndex, int expectedPasses)
    {
        var result = IndexHelper.MoveIndex((ushort)index, spaces, direction);
        Assert.Equal((ushort)expectedIndex, result.Index);
        Assert.Equal((ushort)expectedPasses, result.GoPasses);
    }


    // ─── MoveIndex — lands EXACTLY on GO ────────────────────────────────
    // The token's journey ends on GO. Landing on GO is NOT passing it, so
    // zero passes in either direction.
    //
    // NOTE: the Forward rows are a bug — moving forward onto GO (e.g. 35 → 40)
    // reports GoPasses = 1 because the in-range guard excludes 40, routing it
    // through the wrap branch. Backward landing on GO is already correct.

    [Theory]
    [InlineData(35, 5, PlayerDirection.Forward, 0, 0)]     // 35 → 40 (GO): lands, not a pass
    [InlineData(28, 12, PlayerDirection.Forward, 0, 0)]
    [InlineData(39, 1, PlayerDirection.Forward, 0, 0)]
    [InlineData(12, 12, PlayerDirection.Backward, 0, 0)]   // Double 6 from space 12 — lands on GO
    [InlineData(10, 10, PlayerDirection.Backward, 0, 0)]
    [InlineData(5, 5, PlayerDirection.Backward, 0, 0)]
    public void MoveIndex_LandsExactlyOnGo_IsNotAPass(
        int index, ushort spaces, PlayerDirection direction, int expectedIndex, int expectedPasses)
    {
        var result = IndexHelper.MoveIndex((ushort)index, spaces, direction);
        Assert.Equal((ushort)expectedIndex, result.Index);
        Assert.Equal((ushort)expectedPasses, result.GoPasses);
    }


    // ─── MoveIndex — starts ON GO ───────────────────────────────────────
    // Departing GO counts as passing it ("I'm on GO, I rolled, I've passed GO"):
    // one pass in either direction, since the move ends off GO.
    //
    // NOTE: the Forward rows are a bug — departing GO clockwise reports no pass
    // (index + spaces stays in range, so the in-range guard returns 0). Backward
    // departure is already counted.

    [Theory]
    [InlineData(0, 12, PlayerDirection.Forward, 12, 1)]
    [InlineData(0, 5, PlayerDirection.Forward, 5, 1)]
    [InlineData(0, 12, PlayerDirection.Backward, 28, 1)]
    [InlineData(0, 5, PlayerDirection.Backward, 35, 1)]
    public void MoveIndex_StartsOnGo_IsAPass(
        int index, ushort spaces, PlayerDirection direction, int expectedIndex, int expectedPasses)
    {
        var result = IndexHelper.MoveIndex((ushort)index, spaces, direction);
        Assert.Equal((ushort)expectedIndex, result.Index);
        Assert.Equal((ushort)expectedPasses, result.GoPasses);
    }


    // ─── MoveIndex — negative spaces (back-movement) ────────────────────
    // A negative `spaces` moves |spaces| in the OPPOSITE of the given direction
    // (the Double 3/5/6 back-legs feed MovePlayer negative steps). Index and
    // GoPasses are exactly the equivalent opposite-direction move; whether a
    // back-move's GoPasses is honoured is the caller's call (MovePlayer gates GO
    // on amount > 0).

    [Theory]
    [InlineData(20, -12, PlayerDirection.Forward, 8, 0)]    // back 12, no GO
    [InlineData(8, -3, PlayerDirection.Forward, 5, 0)]      // Double 3 back-leg, no GO
    [InlineData(5, -12, PlayerDirection.Forward, 33, 1)]    // Double 6 — crosses GO
    [InlineData(8, -12, PlayerDirection.Forward, 36, 1)]
    [InlineData(2, -3, PlayerDirection.Forward, 39, 1)]     // back 3 — crosses GO
    [InlineData(12, -12, PlayerDirection.Forward, 0, 0)]    // Double 6 from 12 — lands on GO
    [InlineData(0, -12, PlayerDirection.Forward, 28, 1)]    // back 12 off GO
    [InlineData(35, -7, PlayerDirection.Backward, 2, 1)]    // back-facing: negative goes forward, over GO
    public void MoveIndex_NegativeSpaces_MovesOppositeDirection(
        int index, int spaces, PlayerDirection direction, int expectedIndex, int expectedPasses)
    {
        var result = IndexHelper.MoveIndex((ushort)index, spaces, direction);
        Assert.Equal((ushort)expectedIndex, result.Index);
        Assert.Equal((ushort)expectedPasses, result.GoPasses);
    }

    // Moving -n in a direction is identical to moving +n in the opposite one.
    [Theory]
    [InlineData(5, 12)]
    [InlineData(12, 12)]
    [InlineData(20, 7)]
    [InlineData(0, 12)]
    [InlineData(8, 3)]
    [InlineData(2, 18)]
    public void MoveIndex_NegativeSpaces_EqualsPositiveInOppositeDirection(int index, int n)
    {
        Assert.Equal(
            IndexHelper.MoveIndex((ushort)index, n, PlayerDirection.Backward),
            IndexHelper.MoveIndex((ushort)index, -n, PlayerDirection.Forward));

        Assert.Equal(
            IndexHelper.MoveIndex((ushort)index, n, PlayerDirection.Forward),
            IndexHelper.MoveIndex((ushort)index, -n, PlayerDirection.Backward));
    }


    // ─── AdvanceIndex — no GO involved ──────────────────────────────────
    // Destination already known; moving to it doesn't cross GO.

    [Theory]
    // Forward (destination ahead of current, no wrap)
    [InlineData(5, 10, PlayerDirection.Forward, false)]
    [InlineData(10, 39, PlayerDirection.Forward, false)]
    [InlineData(1, 2, PlayerDirection.Forward, false)]
    // Backward (destination behind current, no wrap)
    [InlineData(20, 8, PlayerDirection.Backward, false)]
    [InlineData(39, 27, PlayerDirection.Backward, false)]
    [InlineData(33, 1, PlayerDirection.Backward, false)]
    [InlineData(12, 5, PlayerDirection.Backward, false)]
    public void AdvanceIndex_NoGoCrossing_NotPassed(
        int current, int desired, PlayerDirection direction, bool expectedPassesGo)
    {
        var result = IndexHelper.AdvanceIndex((ushort)current, (ushort)desired, direction);
        Assert.Equal((ushort)desired, result.Index);
        Assert.Equal(expectedPassesGo, result.PassesGo);
    }


    // ─── AdvanceIndex — crosses GO ──────────────────────────────────────
    // Destination is on the far side of GO from current.

    [Theory]
    // Forward (wrapped: destination at/behind current)
    [InlineData(35, 5, PlayerDirection.Forward, true)]
    [InlineData(39, 2, PlayerDirection.Forward, true)]
    [InlineData(30, 10, PlayerDirection.Forward, true)]
    // Backward (wrapped: destination at/ahead of current)
    [InlineData(5, 33, PlayerDirection.Backward, true)]
    [InlineData(11, 39, PlayerDirection.Backward, true)]
    [InlineData(8, 36, PlayerDirection.Backward, true)]
    [InlineData(1, 29, PlayerDirection.Backward, true)]
    public void AdvanceIndex_CrossesGo_Passed(
        int current, int desired, PlayerDirection direction, bool expectedPassesGo)
    {
        var result = IndexHelper.AdvanceIndex((ushort)current, (ushort)desired, direction);
        Assert.Equal((ushort)desired, result.Index);
        Assert.Equal(expectedPassesGo, result.PassesGo);
    }


    // ─── AdvanceIndex — destination IS GO ───────────────────────────────
    // Arriving on GO is a landing, not a pass: false in either direction.
    //
    // NOTE: the Forward rows are a bug — advancing forward onto GO reports a
    // pass (desired 0 <= current is true). Backward landing on GO is correct.

    [Theory]
    [InlineData(28, 0, PlayerDirection.Forward, false)]
    [InlineData(39, 0, PlayerDirection.Forward, false)]
    [InlineData(12, 0, PlayerDirection.Backward, false)]
    [InlineData(5, 0, PlayerDirection.Backward, false)]
    [InlineData(10, 0, PlayerDirection.Backward, false)]
    public void AdvanceIndex_LandsOnGo_IsNotAPass(
        int current, int desired, PlayerDirection direction, bool expectedPassesGo)
    {
        var result = IndexHelper.AdvanceIndex((ushort)current, (ushort)desired, direction);
        Assert.Equal((ushort)desired, result.Index);
        Assert.Equal(expectedPassesGo, result.PassesGo);
    }


    // ─── AdvanceIndex — starts ON GO ────────────────────────────────────
    // Departing GO counts as passing it: true in either direction.

    [Theory]
    [InlineData(0, 12, PlayerDirection.Forward, true)]
    [InlineData(0, 5, PlayerDirection.Forward, true)]
    [InlineData(0, 28, PlayerDirection.Backward, true)]
    [InlineData(0, 35, PlayerDirection.Backward, true)]
    public void AdvanceIndex_StartsOnGo_IsAPass(
        int current, int desired, PlayerDirection direction, bool expectedPassesGo)
    {
        var result = IndexHelper.AdvanceIndex((ushort)current, (ushort)desired, direction);
        Assert.Equal((ushort)desired, result.Index);
        Assert.Equal(expectedPassesGo, result.PassesGo);
    }


    // ─── AdvanceIndex — guards ──────────────────────────────────────────

    [Theory]
    [InlineData(5, 41, PlayerDirection.Forward)]
    [InlineData(5, 50, PlayerDirection.Backward)]
    public void AdvanceIndex_DesiredBeyondBoard_Throws(int current, int desired, PlayerDirection direction)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => IndexHelper.AdvanceIndex((ushort)current, (ushort)desired, direction));
    }

    [Fact]
    public void AdvanceIndex_JailDesired_IsAllowed()
    {
        // Jail (the virtual index) is the one out-of-board value that must not throw.
        var result = IndexHelper.AdvanceIndex(10, IndexHelper.JailSpace, PlayerDirection.Forward);
        Assert.Equal(IndexHelper.JailSpace, result.Index);
    }
}