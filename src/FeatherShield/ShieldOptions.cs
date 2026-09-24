namespace FeatherShield;

public sealed record ShieldOptions
{
    public bool Enabled { get; init; } = true;
    public bool StrictBlocking { get; init; } = true;
    public bool BlockThirdPartyTrackers { get; init; } = true;
    public IReadOnlyCollection<string> AllowlistedSites { get; init; } = Array.Empty<string>();
}
