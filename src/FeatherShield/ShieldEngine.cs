using FeatherShield.Matching;
using FeatherShield.Tracking;

namespace FeatherShield;

public sealed class ShieldEngine
{
    private static readonly HashSet<ResourceType> StrictTrackerResourceTypes =
    [
        ResourceType.Script,
        ResourceType.Image,
        ResourceType.XmlHttpRequest,
        ResourceType.Fetch,
        ResourceType.Ping,
        ResourceType.Media
    ];

    public ShieldEngine(RuleSet? rules = null)
    {
        Rules = rules ?? new RuleSet();
    }

    public RuleSet Rules { get; }

    public BlockDecision Evaluate(ResourceRequest request, ShieldOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(request);
        options ??= new ShieldOptions();

        if (!options.Enabled || !IsHttp(request.Url))
            return BlockDecision.Allow();

        string documentHost = request.DocumentUrl?.Host ?? string.Empty;

        if (IsAllowlisted(documentHost, options.AllowlistedSites))
            return BlockDecision.Allow(BlockReason.Allowlisted, documentHost);

        string? exception = MatchException(request.Url);
        if (exception is not null)
            return BlockDecision.Allow(BlockReason.ExceptionRule, exception);

        string? domain = MatchBlockedDomain(request.Url.Host);
        if (domain is not null)
            return BlockDecision.Block(BlockReason.BlockedDomain, domain);

        string? urlRule = Rules.UrlRules.FirstOrDefault(rule => rule.IsMatch(request.Url.AbsoluteUri))?.Pattern;
        if (urlRule is not null)
            return BlockDecision.Block(BlockReason.UrlRule, urlRule);

        if (!options.StrictBlocking || request.Type == ResourceType.Document)
            return BlockDecision.Allow();

        bool thirdParty = documentHost.Length > 0 &&
                          !HostMatcher.Related(request.Url.Host, documentHost);
        if (!thirdParty)
            return BlockDecision.Allow();

        string? trackerToken = Rules.TrackingTokens.FirstOrDefault(token =>
            request.Url.AbsoluteUri.Contains(token, StringComparison.OrdinalIgnoreCase));

        if (trackerToken is null)
            return BlockDecision.Allow();

        if (options.BlockThirdPartyTrackers || StrictTrackerResourceTypes.Contains(request.Type))
            return BlockDecision.Block(BlockReason.ThirdPartyTracker, trackerToken);

        return BlockDecision.Allow();
    }

    public string CleanTopLevelUrl(string address) =>
        TrackingParameterCleaner.Clean(address, Rules.TrackingParameters);

    public bool IsAllowlisted(string? host, IReadOnlyCollection<string>? allowlist)
    {
        if (string.IsNullOrWhiteSpace(host) || allowlist is null)
            return false;

        return allowlist
            .Select(HostMatcher.Normalize)
            .Where(static value => value.Length > 0)
            .Any(value => HostMatcher.Matches(host, value));
    }

    private string? MatchException(Uri request)
    {
        string? domain = Rules.ExceptionDomains.FirstOrDefault(rule =>
            HostMatcher.Matches(request.Host, rule));
        if (domain is not null)
            return domain;

        return Rules.ExceptionUrlRules
            .FirstOrDefault(rule => rule.IsMatch(request.AbsoluteUri))?
            .Pattern;
    }

    private string? MatchBlockedDomain(string host) =>
        Rules.BlockedDomains.FirstOrDefault(domain => HostMatcher.Matches(host, domain));

    private static bool IsHttp(Uri uri) =>
        uri.Scheme.Equals(Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase);
}
