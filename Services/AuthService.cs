using System.Security.Cryptography;
using System.Text;
using SevenzyX.Models;

namespace SevenzyX.Services;

public sealed class AuthService
{
    private sealed class Account
    {
        public required string Username { get; init; }
        public required string Hash { get; init; }
        public bool IsAdmin { get; init; }
        public bool IsBlocked { get; set; }
        public DateTime? ExpiresAtUtc { get; set; }
    }

    private readonly List<Account> _users = new()
    {
        new Account { Username = "admin", Hash = Hash("7zyx-admin"), IsAdmin = true },
        new Account { Username = "buyer", Hash = Hash("7zyx-buyer"), IsAdmin = false, ExpiresAtUtc = DateTime.UtcNow.AddDays(30) }
    };

    public string LastError { get; private set; } = "";

    public (bool Ok, bool IsAdmin) Login(string username, string password)
    {
        LastError = "";
        var user = Find(username);

        if (user is null || user.Hash != Hash(password))
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
        _users.Select(x => new UserAccountInfo
        {
            Username = x.Username,
            IsAdmin = x.IsAdmin,
            IsBlocked = x.IsBlocked,
            ExpiresAtUtc = x.ExpiresAtUtc
        }).ToList();

    public bool CreateBuyer(string username, string password, int validityDays, out string error)
    {
        error = "";
        username = username.Trim();

        if (username.Length < 3) { error = "Usuário muito curto."; return false; }
        if (password.Length < 6) { error = "Senha deve ter ao menos 6 caracteres."; return false; }
        if (validityDays < 1 || validityDays > 3650) { error = "Validade inválida."; return false; }
        if (Find(username) is not null) { error = "Usuário já existe."; return false; }

        _users.Add(new Account
        {
            Username = username,
            Hash = Hash(password),
            IsAdmin = false,
            IsBlocked = false,
            ExpiresAtUtc = DateTime.UtcNow.AddDays(validityDays)
        });

        return true;
    }

    public bool ToggleBlocked(string username, out string error)
    {
        error = "";
        var user = Find(username);
        if (user is null) { error = "Conta não encontrada."; return false; }
        if (user.IsAdmin) { error = "Admin principal não pode ser bloqueado."; return false; }

        user.IsBlocked = !user.IsBlocked;
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
        return true;
    }

    public bool DeleteBuyer(string username, out string error)
    {
        error = "";
        var user = Find(username);

        if (user is null) { error = "Conta não encontrada."; return false; }
        if (user.IsAdmin) { error = "Admin principal não pode ser removido."; return false; }

        _users.Remove(user);
        return true;
    }

    private Account? Find(string username) =>
        _users.FirstOrDefault(x => string.Equals(x.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));

    private static string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}