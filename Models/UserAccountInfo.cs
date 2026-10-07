namespace SevenzyX.Models;

public sealed class UserAccountInfo
{
    public required string Username { get; init; }
    public bool IsAdmin { get; init; }
    public bool IsBlocked { get; init; }
    public DateTime? ExpiresAtUtc { get; init; }

    public string Role => IsAdmin ? "Admin" : "Comprador";

    public string Status
    {
        get
        {
            if (IsBlocked) return "Bloqueado";
            if (!IsAdmin && ExpiresAtUtc is DateTime expiry && expiry <= DateTime.UtcNow) return "Expirado";
            return "Ativo";
        }
    }

    public string Expiration => IsAdmin
        ? "Sem expiração"
        : ExpiresAtUtc?.ToLocalTime().ToString("dd/MM/yyyy HH:mm") ?? "Sem validade";
}
