using FeatherShield.Rules;

namespace FeatherShield.Matching;

internal sealed class CosmeticIndex
{
    private readonly List<string> _globalHide = [];
    private readonly List<string> _globalExceptions = [];
    private readonly List<CosmeticRule> _conditionalHide = [];
    private readonly List<CosmeticRule> _conditionalExceptions = [];

    private readonly Dictionary<string, List<CosmeticRule>> _hostHide =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, List<CosmeticRule>> _hostExceptions =
        new(StringComparer.OrdinalIgnoreCase);

    public int Count { get; private set; }

    public void Add(
        string selector,
        bool isException,
        string[]? includedDomains,
        string[]? excludedDomains)
    {
        Count++;

        if (includedDomains is not { Length: > 0 })
        {
            if (excludedDomains is not { Length: > 0 })
            {
                (isException ? _globalExceptions : _globalHide).Add(selector);
                return;
            }

            var conditional = new CosmeticRule(selector, excludedDomains);
            (isException ? _conditionalExceptions : _conditionalHide).Add(conditional);
            return;
        }

        var rule = new CosmeticRule(selector, excludedDomains);
        Dictionary<string, List<CosmeticRule>> index = isException
            ? _hostExceptions
            : _hostHide;

        for (int i = 0; i < includedDomains.Length; i++)
        {
            string domain = includedDomains[i];

            if (!index.TryGetValue(domain, out List<CosmeticRule>? bucket))
            {
                bucket = [];
                index[domain] = bucket;
            }

            bucket.Add(rule);
        }
    }

    public IReadOnlyList<string> GetSelectors(string? host)
    {
        host = string.IsNullOrWhiteSpace(host)
            ? string.Empty
            : DomainMatcher.Normalize(host);

        bool hasConditionalHide = _conditionalHide.Count > 0;
        bool hasHostHide = HasHostEntries(_hostHide, host);
        bool hasAnyExceptions = _globalExceptions.Count > 0 ||
                                _conditionalExceptions.Count > 0 ||
                                HasHostEntries(_hostExceptions, host);

        if (!hasConditionalHide && !hasHostHide && !hasAnyExceptions)
            return _globalHide;

        HashSet<string>? exceptions = BuildExceptions(host);
        var result = new List<string>(_globalHide.Count + 32);

        AddSelectors(_globalHide, exceptions, result);

        for (int i = 0; i < _conditionalHide.Count; i++)
        {
            CosmeticRule rule = _conditionalHide[i];
            if (rule.AppliesTo(host) && !IsExcepted(rule.Selector, exceptions))
                result.Add(rule.Selector);
        }

        AddHostRules(host, exceptions, result);
        return result;
    }

    public void Compact()
    {
        _globalHide.TrimExcess();
        _globalExceptions.TrimExcess();
        _conditionalHide.TrimExcess();
        _conditionalExceptions.TrimExcess();

        CompactIndex(_hostHide);
        CompactIndex(_hostExceptions);
    }

    private HashSet<string>? BuildExceptions(string host)
    {
        HashSet<string>? result = null;

        for (int i = 0; i < _globalExceptions.Count; i++)
            (result ??= new HashSet<string>(StringComparer.Ordinal)).Add(_globalExceptions[i]);

        for (int i = 0; i < _conditionalExceptions.Count; i++)
        {
            CosmeticRule rule = _conditionalExceptions[i];
            if (rule.AppliesTo(host))
                (result ??= new HashSet<string>(StringComparer.Ordinal)).Add(rule.Selector);
        }

        AddHostExceptionsForKey(host, host, ref result);

        int dot = host.IndexOf('.');
        while (dot >= 0 && dot + 1 < host.Length)
        {
            string suffix = host[(dot + 1)..];
            AddHostExceptionsForKey(suffix, host, ref result);
            dot = host.IndexOf('.', dot + 1);
        }

        return result;
    }

    private void AddHostExceptionsForKey(
        string key,
        string host,
        ref HashSet<string>? result)
    {
        if (!_hostExceptions.TryGetValue(key, out List<CosmeticRule>? bucket))
            return;

        for (int i = 0; i < bucket.Count; i++)
        {
            CosmeticRule rule = bucket[i];
            if (rule.AppliesTo(host))
                (result ??= new HashSet<string>(StringComparer.Ordinal)).Add(rule.Selector);
        }
    }

    private void AddHostRules(
        string host,
        HashSet<string>? exceptions,
        List<string> result)
    {
        AddHostRulesForKey(host, host, exceptions, result);

        int dot = host.IndexOf('.');
        while (dot >= 0 && dot + 1 < host.Length)
        {
            string suffix = host[(dot + 1)..];
            AddHostRulesForKey(suffix, host, exceptions, result);
            dot = host.IndexOf('.', dot + 1);
        }
    }

    private void AddHostRulesForKey(
        string key,
        string host,
        HashSet<string>? exceptions,
        List<string> result)
    {
        if (!_hostHide.TryGetValue(key, out List<CosmeticRule>? bucket))
            return;

        for (int i = 0; i < bucket.Count; i++)
        {
            CosmeticRule rule = bucket[i];
            if (rule.AppliesTo(host) && !IsExcepted(rule.Selector, exceptions))
                result.Add(rule.Selector);
        }
    }

    private static bool HasHostEntries(
        Dictionary<string, List<CosmeticRule>> index,
        string host)
    {
        if (host.Length == 0)
            return false;

        if (index.ContainsKey(host))
            return true;

        int dot = host.IndexOf('.');
        while (dot >= 0 && dot + 1 < host.Length)
        {
            if (index.ContainsKey(host[(dot + 1)..]))
                return true;

            dot = host.IndexOf('.', dot + 1);
        }

        return false;
    }

    private static void AddSelectors(
        List<string> selectors,
        HashSet<string>? exceptions,
        List<string> result)
    {
        for (int i = 0; i < selectors.Count; i++)
        {
            string selector = selectors[i];
            if (!IsExcepted(selector, exceptions))
                result.Add(selector);
        }
    }

    private static bool IsExcepted(string selector, HashSet<string>? exceptions) =>
        exceptions is not null && exceptions.Contains(selector);

    private static void CompactIndex(Dictionary<string, List<CosmeticRule>> index)
    {
        foreach (List<CosmeticRule> bucket in index.Values)
            bucket.TrimExcess();

        index.TrimExcess();
    }
}
