namespace FeatherShield.Scriptlets;

internal sealed class ScriptletIndex
{
    private readonly List<ScriptletRule> _generic = [];
    private readonly Dictionary<string, List<ScriptletRule>> _byDomain = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<ScriptletRule> _exceptions = [];

    public int Count { get; private set; }

    public void Add(ScriptletRule rule)
    {
        Count++;

        if (rule.IsException)
        {
            _exceptions.Add(rule);
            return;
        }

        if (rule.IncludedDomains is not { Length: > 0 } domains)
        {
            _generic.Add(rule);
            return;
        }

        foreach (string domain in domains)
        {
            if (!_byDomain.TryGetValue(domain, out List<ScriptletRule>? bucket))
                _byDomain[domain] = bucket = [];

            bucket.Add(rule);
        }
    }

    public IReadOnlyList<ScriptletCall> GetCalls(string? host)
    {
        string value = (host ?? string.Empty).Trim().TrimEnd('.').ToLowerInvariant();
        if (value.Length == 0)
            return [];

        HashSet<string>? disabled = null;
        foreach (ScriptletRule exception in _exceptions)
        {
            if (!exception.AppliesTo(value))
                continue;

            if (exception.DisablesAll)
                return [];

            (disabled ??= new HashSet<string>(StringComparer.Ordinal)).Add(exception.Call.Signature);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var calls = new List<ScriptletCall>();

        void Consider(ScriptletRule rule)
        {
            if (!rule.AppliesTo(value))
                return;

            string signature = rule.Call.Signature;
            if (disabled?.Contains(signature) == true || !seen.Add(signature))
                return;

            calls.Add(rule.Call);
        }

        foreach (ScriptletRule rule in _generic)
            Consider(rule);

        int start = 0;
        while (start < value.Length)
        {
            if (_byDomain.TryGetValue(value[start..], out List<ScriptletRule>? bucket))
            {
                foreach (ScriptletRule rule in bucket)
                    Consider(rule);
            }

            int dot = value.IndexOf('.', start);
            if (dot < 0)
                break;

            start = dot + 1;
        }

        foreach (string entity in Matching.DomainMatcher.EntityKeys(value))
        {
            if (_byDomain.TryGetValue(entity, out List<ScriptletRule>? bucket))
            {
                foreach (ScriptletRule rule in bucket)
                    Consider(rule);
            }
        }

        return calls;
    }

    public void Compact()
    {
        _generic.TrimExcess();
        _exceptions.TrimExcess();
        _byDomain.TrimExcess();

        foreach (List<ScriptletRule> bucket in _byDomain.Values)
            bucket.TrimExcess();
    }
}
