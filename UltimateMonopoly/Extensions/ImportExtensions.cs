using MP.GameEngine.Enums;

namespace UltimateMonopoly.Extensions;

public static class ImportExtensions
{
    public static BoardSpaceType? ParseBoardSpace(this string boardSpace)
        => Enum.TryParse(boardSpace, true, out BoardSpaceType space) ? space : null;

    public static BoardSpaceType? ParseBoardSpace(this int boardSpace)
        => Enum.IsDefined(typeof(BoardSpaceType), boardSpace) ? (BoardSpaceType)boardSpace : null;
}