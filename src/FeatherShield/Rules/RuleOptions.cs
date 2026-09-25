using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class RuleOptions
{
    public HashSet<ResourceType> IncludedTypes { get; } = [];
    public HashSet<ResourceType> ExcludedTypes { get; } = [];
    public HashSet<string> IncludedDomains { get; } =
        new(StringComparer.OrdinalIgnoreCase);
    public HashSet<string> ExcludedDomains { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool? ThirdParty { get; set; }
    public bool MatchCase { get; set; }
    public bool Important { get; set; }

    public bool Matches(ResourceRequest request)
    {
        if (IncludedTypes.Count > 0 && !IncludedTypes.Contains(request.Type))
            return false;

        if (ExcludedTypes.Contains(request.Type))
            return false;

        string documentHost = request.DocumentUrl?.Host ?? string.Empty;

        if (IncludedDomains.Count > 0 &&
            !IncludedDomains.Any(domain => DomainMatcher.Matches(documentHost, domain)))
        {
            return false;
        }

        if (ExcludedDomains.Any(domain => DomainMatcher.Matches(documentHost, domain)))
            return false;

        if (ThirdParty is not null)
        {
            bool thirdParty = DomainMatcher.IsThirdParty(
                request.Url.Host,
                documentHost);

            if (thirdParty != ThirdParty.Value)
                return false;
        }

        return true;
    }
}
