namespace FeatherShield.Rules;

internal sealed class NetworkRule
{
    public required string Source { get; init; }
    public required bool IsException { get; init; }
    public required AbpPattern Pattern { get; init; }
    public required RuleOptions Options { get; init; }

    public string? HostAnchor => Pattern.HostAnchor;

    public bool Matches(ResourceRequest request) =>
        Options.Matches(request) && Pattern.IsMatch(request.Url);
}
