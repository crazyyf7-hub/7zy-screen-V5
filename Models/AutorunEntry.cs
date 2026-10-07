namespace SevenzyX.Models;

public sealed class AutorunEntry
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Command { get; init; }
    public required string Location { get; init; }
    public bool IsEnabled { get; init; }
    public string Status => IsEnabled ? "Ativo" : "Desativado";
}
