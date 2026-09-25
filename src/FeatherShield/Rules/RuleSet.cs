using FeatherShield.Matching;

namespace FeatherShield.Rules;

public sealed class RuleSet
{
    private readonly RuleIndex _blocking = new();
    private readonly RuleIndex _exceptions = new();
    private readonly List<CosmeticRule> _cosmeticRules = [];

    internal List<string> TrackingTokens { get; } = [];
    internal HashSet<string> TrackingParameters { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int NetworkRuleCount => _blocking.Count;
    public int ExceptionRuleCount => _exceptions.Count;
    public int CosmeticRuleCount => _cosmeticRules.Count;
    public int TrackingTokenCount => TrackingTokens.Count;
    public int TrackingParameterCount => TrackingParameters.Count;

    // Compatibility with Feather Browser 1.x adapter diagnostics.
    public int BlockedDomainCount => NetworkRuleCount;
    public int ExceptionDomainCount => ExceptionRuleCount;
    public int UrlRuleCount => NetworkRuleCount;

    internal void Add(NetworkRule rule) =>
        (rule.IsException ? _exceptions : _blocking).Add(rule);

    internal void Add(CosmeticRule rule) =>
        _cosmeticRules.Add(rule);

    internal IEnumerable<NetworkRule> BlockingCandidates(string host) =>
        _blocking.Candidates(host);

    internal IEnumerable<NetworkRule> ExceptionCandidates(string host) =>
        _exceptions.Candidates(host);

    internal IEnumerable<CosmeticRule> CosmeticRules => _cosmeticRules;

    public void AddTrackingToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        if (!TrackingTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
            TrackingTokens.Add(token.Trim());
    }

    public bool AddTrackingParameter(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return TrackingParameters.Add(parameter.Trim());
    }
}
