using MP.GameEngine.Helpers;

namespace UltimateMonopoly.Helpers;

/// <summary>Which edge of the board a space sits on — drives the colour-banner orientation.</summary>
public enum BoardSide
{
    Top,
    Bottom,
    Left,
    Right,
    Corner
}

/// <summary>
/// A space's placement in the 13×13 board grid (1-based column/row) and which
/// side it sits on. Corners span 2×2; edge spaces are one cell along the edge
/// and two cells deep (so corner-depth == edge-depth, the Monopoly look).
/// </summary>
public readonly record struct BoardCell(int Column, int Row, int ColumnSpan, int RowSpan, BoardSide Side);

/// <summary>
/// Maps a physical board index (0–39) to its cell in the 13×13 board grid used
/// by <c>_BoardView</c>. Layout per <c>design-docs/Game-UI.md</c>: 2×2 corners
/// clockwise from GO (bottom-right), nine edge spaces per side, a 9×9 centre.
/// </summary>
public static class BoardLayoutHelper
{
    public static BoardCell GetCell(ushort index)
    {
        // Corners — 2×2.
        if (index == IndexHelper.GoSpace)           return new(12, 12, 2, 2, BoardSide.Corner); // bottom-right
        if (index == IndexHelper.JustVisitingSpace) return new(1, 12, 2, 2, BoardSide.Corner);  // bottom-left
        if (index == IndexHelper.FreeParkingSpace)  return new(1, 1, 2, 2, BoardSide.Corner);   // top-left
        if (index == IndexHelper.GoToJailSpace)     return new(12, 1, 2, 2, BoardSide.Corner);  // top-right

        // Edges — nine spaces per side, between the corners. The deep dimension
        // spans two cells (rows 1–2 / 12–13, or cols 1–2 / 12–13).
        return index switch
        {
            >= 1 and <= 9   => new(12 - index, 12, 1, 2, BoardSide.Bottom), // GO → Just Visiting (right→left)
            >= 11 and <= 19 => new(1, 22 - index, 2, 1, BoardSide.Left),    // Just Visiting → Free Parking (bottom→top)
            >= 21 and <= 29 => new(index - 18, 1, 1, 2, BoardSide.Top),     // Free Parking → Go To Jail (left→right)
            >= 31 and <= 39 => new(12, index - 28, 2, 1, BoardSide.Right),  // Go To Jail → GO (top→bottom)
            _ => throw new ArgumentOutOfRangeException(nameof(index), index, "Not a physical board index (0–39).")
        };
    }
}