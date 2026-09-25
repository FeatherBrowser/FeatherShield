using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class CosmeticRule
{
    public CosmeticRule(string selector, string[]? excludedDomains)
    {
        Selector = selector;
        ExcludedDomains = excludedDomains;
    }

    public string Selector { get; }
    public string[]? ExcludedDomains { get; }

    public bool AppliesTo(string host)
    {
        if (ExcludedDomains is not { Length: > 0 })
            return true;

        for (int i = 0; i < ExcludedDomains.Length; i++)
        {
            if (DomainMatcher.MatchesNormalized(host, ExcludedDomains[i]))
                return false;
        }

        return true;
    }
}
