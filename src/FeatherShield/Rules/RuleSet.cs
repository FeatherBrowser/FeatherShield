using FeatherShield.Matching;

namespace FeatherShield.Rules;

public sealed class RuleSet
{
    private readonly RuleIndex _blocking = new();
    private readonly RuleIndex _exceptions = new();
    private readonly CosmeticIndex _cosmetic = new();
    private readonly TrackingTokenIndex _trackingTokens = new();
    private readonly HashSet<string> _trackingTokenSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ulong> _ruleSignatures = [];

    internal HashSet<string> TrackingParameters { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int NetworkRuleCount => _blocking.Count;
    public int ExceptionRuleCount => _exceptions.Count;
    public int CosmeticRuleCount => _cosmetic.Count;
    public int TrackingTokenCount => _trackingTokenSet.Count;
    public int TrackingParameterCount => TrackingParameters.Count;

    public int BlockedDomainCount => NetworkRuleCount;
    public int ExceptionDomainCount => ExceptionRuleCount;
    public int UrlRuleCount => NetworkRuleCount;

    internal bool TryRegisterRule(string value) =>
        _ruleSignatures.Add(HashRule(value));

    internal void Add(NetworkRule rule) =>
        (rule.IsException ? _exceptions : _blocking).Add(rule);

    internal void AddCosmetic(
        string selector,
        bool isException,
        string[]? includedDomains,
        string[]? excludedDomains) =>
        _cosmetic.Add(selector, isException, includedDomains, excludedDomains);

    internal NetworkRule? FindBlockingMatch(ResourceRequest request) =>
        _blocking.FindMatch(request, preferImportant: true);

    internal NetworkRule? FindExceptionMatch(ResourceRequest request) =>
        _exceptions.FindMatch(request, preferImportant: false);

    internal IReadOnlyList<string> GetCosmeticSelectors(string? host) =>
        _cosmetic.GetSelectors(host);

    internal string? FindTrackingToken(string url) =>
        _trackingTokens.Find(url);

    public void AddTrackingToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);

        string value = token.Trim();
        if (_trackingTokenSet.Add(value))
            _trackingTokens.Add(value);
    }

    public bool AddTrackingParameter(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return TrackingParameters.Add(parameter.Trim());
    }

    public void Optimize()
    {
        _blocking.Compact();
        _exceptions.Compact();
        _cosmetic.Compact();
        _trackingTokens.Compact();
        _trackingTokenSet.TrimExcess();
        _ruleSignatures.TrimExcess();
        TrackingParameters.TrimExcess();
    }

    private static ulong HashRule(string value)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;
        for (int i = 0; i < value.Length; i++)
        {
            hash ^= value[i];
            hash *= prime;
        }

        hash ^= (ulong)value.Length;
        hash *= prime;
        return hash;
    }
}
