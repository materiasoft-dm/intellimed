using System.Security.Cryptography;

namespace IntelliMed.Core.Utilities;

/// <summary>Generates random passwords that satisfy IntelliMed's Identity password policy (upper, lower, digit, special, 8+ chars).</summary>
public static class PasswordGenerator
{
    private const string Upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
    private const string Lower = "abcdefghijkmnpqrstuvwxyz";
    private const string Digits = "23456789";
    private const string Special = "!@#$%^&*";

    public static string Generate(int length = 12)
    {
        var all = Upper + Lower + Digits + Special;
        var result = new char[length];
        result[0] = Upper[RandomNumberGenerator.GetInt32(Upper.Length)];
        result[1] = Lower[RandomNumberGenerator.GetInt32(Lower.Length)];
        result[2] = Digits[RandomNumberGenerator.GetInt32(Digits.Length)];
        result[3] = Special[RandomNumberGenerator.GetInt32(Special.Length)];
        for (var i = 4; i < length; i++)
            result[i] = all[RandomNumberGenerator.GetInt32(all.Length)];

        for (var i = result.Length - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (result[i], result[j]) = (result[j], result[i]);
        }

        return new string(result);
    }
}
