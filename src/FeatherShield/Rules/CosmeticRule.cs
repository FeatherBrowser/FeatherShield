using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class CosmeticRule
{
    public required string Selector { get; init; }
    public required bool IsException { get; init; }

    public HashSet<string> IncludedDomains { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public HashSet<string> ExcludedDomains { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public bool AppliesTo(string host)
    {
        if (IncludedDomains.Count > 0 &&
            !IncludedDomains.Any(domain => DomainMatcher.Matches(host, domain)))
        {
            return false;
        }

        return !ExcludedDomains.Any(domain => DomainMatcher.Matches(host, domain));
    }
}
