using System.Security.Cryptography;
using System.Text.Json;
using SevenzyX.Models;

namespace SevenzyX.Services;

public sealed class AuthService
{
    private sealed class Account
    {
        public string Username { get; set; } = "";
        public string Salt { get; set; } = "";
        public string Hash { get; set; } = "";
        public bool IsAdmin { get; set; }
        public bool IsBlocked { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }

    private readonly string _storePath;
    private List<Account> _users = new();

    public string LastError { get; private set; } = "";

    public AuthService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "7zyX");
        Directory.CreateDirectory(dir);
        _storePath = Path.Combine(dir, "accounts.json");
        LoadOrSeed();
    }

    public (bool Ok, bool IsAdmin) Login(string username, string password)
    {
        LastError = "";
        var user = Find(username);

        if (user is null || !Verify(password, user))
        {
            LastError = "Usuário ou senha inválidos.";
            return (false, false);
        }

        if (user.IsBlocked)
        {
            LastError = "Este acesso está bloqueado.";
            return (false, false);
        }

        if (!user.IsAdmin && user.ExpiresAtUtc is DateTime expiry && expiry <= DateTime.UtcNow)
        {
            LastError = "Este acesso expirou.";
            return (false, false);
        }

        return (true, user.IsAdmin);
    }

    public IReadOnlyList<UserAccountInfo> GetUsers() =>
        _users
            .OrderByDescending(x => x.IsAdmin)
            .ThenBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .Select(x => new UserAccountInfo
            {
                Username = x.Username,
                IsAdmin = x.IsAdmin,
                IsBlocked = x.IsBlocked,
                ExpiresAtUtc = x.ExpiresAtUtc
            })
            .ToList();

    public bool CreateBuyer(string username, string password, int validityDays, out string error)
    {
        error = "";
        username = username.Trim();

        if (username.Length < 3) { error = "Usuário muito curto."; return false; }
        if (password.Length < 6) { error = "Senha deve ter ao menos 6 caracteres."; return false; }
        if (validityDays < 1 || validityDays > 3650) { error = "Validade inválida."; return false; }
        if (Find(username) is not null) { error = "Usuário já existe."; return false; }

        var salt = RandomNumberGenerator.GetBytes(16);
        _users.Add(new Account
        {
            Username = username,
            Salt = Convert.ToBase64String(salt),
            Hash = HashPassword(password, salt),
            IsAdmin = false,
            IsBlocked = false,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(validityDays)
        });

        Save();
        return true;
    }

    public bool ToggleBlocked(string username, out string error)
    {
        error = "";
        var user = Find(username);
        if (user is null) { error = "Conta não encontrada."; return false; }
        if (user.IsAdmin) { error = "Admin principal não pode ser bloqueado."; return false; }

        user.IsBlocked = !user.IsBlocked;
        Save();
        return true;
    }

    public bool SetValidityDays(string username, int days, out string error)
    {
        error = "";
        var user = Find(username);

        if (user is null) { error = "Conta não encontrada."; return false; }
        if (user.IsAdmin) { error = "Admin principal não expira."; return false; }
        if (days < 1 || days > 3650) { error = "Validade inválida."; return false; }

        user.ExpiresAtUtc = DateTime.UtcNow.AddDays(days);
        Save();
        return true;
    }

    public bool DeleteBuyer(string username, out string error)
    {
        error = "";
        var user = Find(username);

        if (user is null) { error = "Conta não encontrada."; return false; }
        if (user.IsAdmin) { error = "Admin principal não pode ser removido."; return false; }

        _users.Remove(user);
        Save();
        return true;
    }

    private Account? Find(string username) =>
        _users.FirstOrDefault(x =>
            string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

    private void LoadOrSeed()
    {
        try
        {
            if (File.Exists(_storePath))
                _users = JsonSerializer.Deserialize<List<Account>>(File.ReadAllText(_storePath)) ?? new();
        }
        catch
        {
            _users = new();
        }

        if (_users.Count > 0) return;

        _users.Add(CreateSeed("admin", "7zyx-admin", true, null));
        _users.Add(CreateSeed("buyer", "7zyx-buyer", false, DateTime.UtcNow.AddDays(30)));
        Save();
    }

    private static Account CreateSeed(string username, string password, bool admin, DateTime? expires)
    {
        var salt = RandomNumberGenerator.GetBytes(16);
        return new Account
        {
            Username = username,
            Salt = Convert.ToBase64String(salt),
            Hash = HashPassword(password, salt),
            IsAdmin = admin,
            ExpiresAtUtc = expires
        };
    }

    private void Save()
    {
        var temp = _storePath + ".tmp";
        var json = JsonSerializer.Serialize(_users, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(temp, json);
        File.Move(temp, _storePath, true);
    }

    private static string HashPassword(string password, byte[] salt)
    {
        var bytes = Rfc2898DeriveBytes.Pbkdf2(
            password,
            salt,
            120_000,
            HashAlgorithmName.SHA256,
            32);
        return Convert.ToBase64String(bytes);
    }

    private static bool Verify(string password, Account user)
    {
        try
        {
            var salt = Convert.FromBase64String(user.Salt);
            var expected = Convert.FromBase64String(user.Hash);
            var actual = Convert.FromBase64String(HashPassword(password, salt));
            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
