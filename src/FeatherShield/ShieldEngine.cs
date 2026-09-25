using FeatherShield.Cosmetic;
using FeatherShield.Matching;
using FeatherShield.Rules;
using FeatherShield.Tracking;

namespace FeatherShield;

public sealed class ShieldEngine
{
    private static readonly HashSet<ResourceType> TrackerResourceTypes =
    [
        ResourceType.Script,
        ResourceType.Image,
        ResourceType.XmlHttpRequest,
        ResourceType.Fetch,
        ResourceType.Ping,
        ResourceType.Media,
        ResourceType.WebSocket
    ];

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
        options ??= new ShieldOptions();

        if (!options.Enabled || !IsHttp(request.Url))
            return BlockDecision.Allow();

        string documentHost = request.DocumentUrl?.Host ?? string.Empty;

        if (IsAllowlisted(documentHost, options.AllowlistedSites))
        {
            return BlockDecision.Allow(
                BlockReason.Allowlisted,
                documentHost);
        }

        NetworkRule? exception = Rules
            .ExceptionCandidates(request.Url.Host)
            .FirstOrDefault(rule => rule.Matches(request));

        if (exception is not null)
        {
            return BlockDecision.Allow(
                BlockReason.ExceptionRule,
                exception.Source);
        }

        NetworkRule? block = Rules
            .BlockingCandidates(request.Url.Host)
            .OrderByDescending(rule => rule.Options.Important)
            .FirstOrDefault(rule => rule.Matches(request));

        if (block is not null)
        {
            return BlockDecision.Block(
                BlockReason.NetworkRule,
                block.Source);
        }

        if (!options.StrictBlocking ||
            request.Type == ResourceType.Document)
        {
            return BlockDecision.Allow();
        }

        bool thirdParty = DomainMatcher.IsThirdParty(
            request.Url.Host,
            documentHost);

        if (!thirdParty)
            return BlockDecision.Allow();

        string? trackerToken = Rules.TrackingTokens.FirstOrDefault(
            token => request.Url.AbsoluteUri.Contains(
                token,
                StringComparison.OrdinalIgnoreCase));

        if (trackerToken is null)
            return BlockDecision.Allow();

        if (options.BlockThirdPartyTrackers ||
            TrackerResourceTypes.Contains(request.Type))
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
        TrackingParameterCleaner.Clean(
            address,
            Rules.TrackingParameters);

    public bool IsAllowlisted(
        string? host,
        IReadOnlyCollection<string>? allowlist)
    {
        if (string.IsNullOrWhiteSpace(host) || allowlist is null)
            return false;

        return allowlist
            .Select(DomainMatcher.Normalize)
            .Where(static value => value.Length > 0)
            .Any(value => DomainMatcher.Matches(host, value));
    }

    private static bool IsHttp(Uri uri) =>
        uri.Scheme.Equals(
            Uri.UriSchemeHttp,
            StringComparison.OrdinalIgnoreCase) ||
        uri.Scheme.Equals(
            Uri.UriSchemeHttps,
            StringComparison.OrdinalIgnoreCase);
}
