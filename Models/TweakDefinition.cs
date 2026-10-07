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
    public required string Category { get; init; }
    public required string Title { get; init; }
    public required string Description { get; init; }
    public TweakRisk Risk { get; init; }
    public bool Selected { get; set; }
    public bool RequiresRestart { get; init; }

    public string RiskLabel => Risk switch
    {
        TweakRisk.Safe => "Safe",
        TweakRisk.Moderate => "Moderate",
        _ => "Aggressive"
    };

    public string RestartLabel => RequiresRestart ? "Reinício recomendado" : "";
}
