namespace SevenzyX.Models;

public enum TweakRisk
{
    Safe,
    Moderate,
    Aggressive
}

public sealed class TweakDefinition
{
    public required string Id { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public TweakRisk Risk { get; init; }
    public bool Selected { get; set; }
}