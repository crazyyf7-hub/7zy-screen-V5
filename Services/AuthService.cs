using System.Security.Cryptography;
using System.Text;

namespace SevenzyX.Services;

public sealed class AuthService
{
    private readonly Dictionary<string, (string Hash, bool IsAdmin)> _users =
        new(StringComparer.OrdinalIgnoreCase);

    public AuthService()
    {
        _users["admin"] = (Hash("7zyx-admin"), true);
        _users["buyer"] = (Hash("7zyx-buyer"), false);
    }

    public (bool Ok, bool IsAdmin) Login(string username, string password)
    {
        if (!_users.TryGetValue(username.Trim(), out var entry))
            return (false, false);

        return (entry.Hash == Hash(password), entry.IsAdmin);
    }

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}