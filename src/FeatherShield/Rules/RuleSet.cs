using FeatherShield.Matching;
using FeatherShield.Scriptlets;

namespace FeatherShield.Rules;

public sealed class RuleSet
{
    private readonly RuleIndex _important = new();
    private readonly RuleIndex _blocking = new();
    private readonly RuleIndex _exceptions = new();
    private readonly RuleIndex _redirects = new();
    private readonly RuleIndex _redirectExceptions = new();
    private readonly CosmeticIndex _cosmetic = new();
    private readonly ScriptletIndex _scriptlets = new();
    private readonly TrackingTokenIndex _trackingTokens = new();
    private readonly HashSet<string> _trackingTokenSet = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<ulong> _ruleSignatures = [];

    internal HashSet<string> TrackingParameters { get; } =
        new(StringComparer.OrdinalIgnoreCase);

    public int NetworkRuleCount => _blocking.Count + _important.Count;
    public int ExceptionRuleCount => _exceptions.Count;
    public int CosmeticRuleCount => _cosmetic.Count;
    public int RedirectRuleCount => _redirects.Count;
    public int ScriptletRuleCount => _scriptlets.Count;
    public int TrackingTokenCount => _trackingTokenSet.Count;
    public int TrackingParameterCount => TrackingParameters.Count;

    public int BlockedDomainCount => NetworkRuleCount;
    public int ExceptionDomainCount => ExceptionRuleCount;
    public int UrlRuleCount => NetworkRuleCount;

    internal bool TryRegisterRule(string value) =>
        _ruleSignatures.Add(HashRule(value));

    internal void Add(NetworkRule rule) =>
        (rule.IsException ? _exceptions : rule.Options.Important ? _important : _blocking).Add(rule);

    internal void AddCosmetic(
        string selector,
        bool isException,
        string[]? includedDomains,
        string[]? excludedDomains) =>
        _cosmetic.Add(selector, isException, includedDomains, excludedDomains);

    internal void AddRedirect(NetworkRule rule) => _redirects.Add(rule);

    internal void AddRedirectException(NetworkRule rule) => _redirectExceptions.Add(rule);

    internal void AddScriptlet(ScriptletRule rule) => _scriptlets.Add(rule);

    internal IReadOnlyList<ScriptletCall> GetScriptlets(string? host) => _scriptlets.GetCalls(host);

    internal string? FindRedirect(ResourceRequest request)
    {
        NetworkRule? redirect = _redirects.FindMatch(request, preferImportant: false);
        if (redirect?.Redirect is not string resource)
            return null;

        if (!redirect.Options.Important &&
            _redirectExceptions.FindMatch(request, preferImportant: false) is { Redirect: string cancelled } &&
            (cancelled == RedirectResources.None || cancelled == resource))
        {
            return null;
        }

        return resource;
    }

    internal NetworkRule? FindBlockingMatch(ResourceRequest request) =>
        _important.FindMatch(request, preferImportant: false) ??
        _blocking.FindMatch(request, preferImportant: false);

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
        _important.Compact();
        _blocking.Compact();
        _exceptions.Compact();
        _redirects.Compact();
        _redirectExceptions.Compact();
        _cosmetic.Compact();
        _scriptlets.Compact();
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
