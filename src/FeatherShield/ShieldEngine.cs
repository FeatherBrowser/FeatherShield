using FeatherShield.Cosmetic;
using FeatherShield.Matching;
using FeatherShield.Rules;
using FeatherShield.Tracking;

namespace FeatherShield;

public sealed class ShieldEngine
{
    private static readonly ShieldOptions DefaultOptions = new();

    public ShieldEngine(RuleSet? rules = null)
    {
        Rules = rules ?? new RuleSet();
    }

    public RuleSet Rules { get; }

    public BlockDecision Evaluate(
        ResourceRequest request,
        ShieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= DefaultOptions;

        if (!options.Enabled || !IsHttp(request.Url))
            return BlockDecision.Allow();

        string documentHost = request.DocumentUrl?.Host ?? string.Empty;

        if (IsAllowlisted(documentHost, options.AllowlistedSites))
        {
            return BlockDecision.Allow(
                BlockReason.Allowlisted,
                documentHost);
        }

        NetworkRule? block = Rules.FindBlockingMatch(request);
        if (block is not null && block.Options.Important)
        {
            return BlockDecision.Block(
                BlockReason.NetworkRule,
                block.Source);
        }

        NetworkRule? exception = Rules.FindExceptionMatch(request);
        if (exception is not null)
        {
            return BlockDecision.Allow(
                BlockReason.ExceptionRule,
                exception.Source);
        }

        if (block is not null)
        {
            return BlockDecision.Block(
                BlockReason.NetworkRule,
                block.Source);
        }

        if (!options.StrictBlocking || request.Type == ResourceType.Document)
            return BlockDecision.Allow();

        if (!DomainMatcher.IsThirdParty(request.Url.Host, documentHost))
            return BlockDecision.Allow();

        string? trackerToken = Rules.FindTrackingToken(request.Url.AbsoluteUri);
        if (trackerToken is null)
            return BlockDecision.Allow();

        if (options.BlockThirdPartyTrackers || IsTrackerResourceType(request.Type))
        {
            return BlockDecision.Block(
                BlockReason.ThirdPartyTracker,
                trackerToken);
        }

        return BlockDecision.Allow();
    }

    public string GetCosmeticFilterScript(string? pageAddress)
    {
        string host = string.Empty;

        if (Uri.TryCreate(pageAddress, UriKind.Absolute, out Uri? uri))
            host = uri.Host;

        return CosmeticFilterBuilder.BuildInjectionScript(Rules, host);
    }

    public IReadOnlyList<string> GetCosmeticSelectors(string? host) =>
        CosmeticFilterBuilder.GetSelectors(Rules, host);

    public string CleanTopLevelUrl(string address) =>
        TrackingParameterCleaner.Clean(address, Rules.TrackingParameters);

    public bool IsAllowlisted(
        string? host,
        IReadOnlyCollection<string>? allowlist)
    {
        if (string.IsNullOrWhiteSpace(host) || allowlist is null || allowlist.Count == 0)
            return false;

        foreach (string value in allowlist)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            string domain = DomainMatcher.Normalize(value);
            if (domain.Length > 0 && DomainMatcher.MatchesNormalized(host, domain))
                return true;
        }

        return false;
    }

    private static bool IsTrackerResourceType(ResourceType type) =>
        type is ResourceType.Script or
            ResourceType.Image or
            ResourceType.XmlHttpRequest or
            ResourceType.Fetch or
            ResourceType.Ping or
            ResourceType.Media or
            ResourceType.WebSocket;

    private static bool IsHttp(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
