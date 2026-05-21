using System.Security.Cryptography;

namespace UltimateMonopoly.Helpers;

public class JoinCodeGenerator
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
    public static string New() => new string(RandomNumberGenerator.GetItems<char>(Alphabet, 5));
}