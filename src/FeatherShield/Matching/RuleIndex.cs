using FeatherShield.Matching;
using FeatherShield.Rules;

namespace FeatherShield.Matching;

internal sealed class RuleIndex
{
    private readonly Dictionary<string, List<NetworkRule>> _hostRules =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly List<NetworkRule> _genericRules = [];

    public int Count { get; private set; }

    public void Add(NetworkRule rule)
    {
        Count++;

        if (string.IsNullOrWhiteSpace(rule.HostAnchor))
        {
            _genericRules.Add(rule);
            return;
        }

        if (!_hostRules.TryGetValue(rule.HostAnchor, out List<NetworkRule>? bucket))
        {
            bucket = [];
            _hostRules[rule.HostAnchor] = bucket;
        }

        bucket.Add(rule);
    }

    public IEnumerable<NetworkRule> Candidates(string host)
    {
        foreach (string suffix in DomainMatcher.EnumerateSuffixes(host))
        {
            if (_hostRules.TryGetValue(suffix, out List<NetworkRule>? rules))
            {
                foreach (NetworkRule rule in rules)
                    yield return rule;
            }
        }

        foreach (NetworkRule rule in _genericRules)
            yield return rule;
    }
}
