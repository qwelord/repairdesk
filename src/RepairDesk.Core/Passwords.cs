using System.Security.Cryptography;

namespace RepairDesk.Core;

public static class Passwords
{
    public static string Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, 210000, HashAlgorithmName.SHA256, 32);
        return $"210000:{Convert.ToBase64String(salt)}:{Convert.ToBase64String(hash)}";
    }

    public static bool Verify(string password, string encoded)
    {
        try
        {
            var pieces = encoded.Split(':');
            if (pieces.Length != 3 || !int.TryParse(pieces[0], out var iterations) || iterations is < 100000 or > 1000000)
                return false;
            var expected = Convert.FromBase64String(pieces[2]);
            var hash = Rfc2898DeriveBytes.Pbkdf2(password, Convert.FromBase64String(pieces[1]), iterations, HashAlgorithmName.SHA256, expected.Length);
            return CryptographicOperations.FixedTimeEquals(hash, expected);
        }
        catch (FormatException) { return false; }
    }
}
