namespace UltimateMonopoly.Helpers;

/// <summary>Formats a byte count as a human-readable size. Always KB or MB (never raw bytes):
/// below 1024 KB shows KB, otherwise MB. Up to 2 decimal places, trailing zeros dropped
/// (so "12 KB", "1.4 MB", "1.42 MB").</summary>
public static class FileSizeFormatter
{
    public static string Format(long bytes)
    {
        var kb = bytes / 1024d;
        return kb < 1024
            ? $"{kb:0.##} KB"
            : $"{kb / 1024d:0.##} MB";
    }
}