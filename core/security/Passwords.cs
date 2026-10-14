using System.Security.Cryptography;

namespace Hydra.Core.Security;

public static class Passwords
{
    private const int Iterations = 600_000;
    public static string Hash(string password)
    {
        if (password.Length < 12 || password.Length > 1024) throw new ArgumentException("Use a password between 12 and 1024 characters.");
        var salt = RandomNumberGenerator.GetBytes(16);
        var key = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, 32);
        return $"pbkdf2-sha256${Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(key)}";
    }
    public static bool Verify(string password, string encoded)
    {
        if (password.Length > 1024) return false;
        try
        {
            var parts = encoded.Split('$');
            if (parts.Length != 4 || parts[0] != "pbkdf2-sha256" || !int.TryParse(parts[1], out var cost) || cost is < 100_000 or > 2_000_000) return false;
            var salt = Convert.FromBase64String(parts[2]); var expected = Convert.FromBase64String(parts[3]);
            if (salt.Length != 16 || expected.Length != 32) return false;
            return CryptographicOperations.FixedTimeEquals(expected, Rfc2898DeriveBytes.Pbkdf2(password, salt, cost, HashAlgorithmName.SHA256, 32));
        }
        catch (FormatException) { return false; }
    }
}
