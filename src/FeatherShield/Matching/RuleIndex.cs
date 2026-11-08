using FeatherShield.Rules;

namespace FeatherShield.Matching;

internal sealed class RuleIndex
{
    private readonly Dictionary<ulong, NetworkRule> _hostPrimary = [];
    private Dictionary<ulong, List<NetworkRule>>? _hostOverflow;

    private readonly Dictionary<ulong, NetworkRule> _keyPrimary = [];
    private Dictionary<ulong, List<NetworkRule>>? _keyOverflow;

    private readonly List<NetworkRule> _fallback = [];

    private readonly Dictionary<string, List<NetworkRule>> _byDocumentDomain = new(StringComparer.OrdinalIgnoreCase);
    private bool _hasEntityDocumentDomains;

    public int Count { get; private set; }

    public void Add(NetworkRule rule)
    {
        Count++;

        if (rule.HostAnchor is string host)
        {
            AddHostRule(host, rule);
            return;
        }

        if (rule.Pattern.TryGetIndexKey(out ulong key))
        {
            AddKeyRule(key, rule);
            return;
        }

        if (rule.Options.IncludedDomains is { Length: > 0 } domains)
        {
            foreach (string domain in domains)
            {
                if (!_byDocumentDomain.TryGetValue(domain, out List<NetworkRule>? bucket))
                    _byDocumentDomain[domain] = bucket = [];

                bucket.Add(rule);
                _hasEntityDocumentDomains |= domain.EndsWith(".*", StringComparison.Ordinal);
            }

            return;
        }

        _fallback.Add(rule);
    }

    public NetworkRule? FindMatch(ResourceRequest request, bool preferImportant)
    {
        NetworkRule? firstNormal = null;

        if (TryScanHostRules(request, preferImportant, ref firstNormal, out NetworkRule? match))
            return match;

        if (TryScanIndexedRules(request, preferImportant, ref firstNormal, out match))
            return match;

        if (TryScanList(_fallback, request, preferImportant, ref firstNormal, out match))
            return match;

        if (_byDocumentDomain.Count > 0 &&
            TryScanDocumentDomainRules(request, preferImportant, ref firstNormal, out match))
        {
            return match;
        }

        return firstNormal;
    }

    private bool TryScanDocumentDomainRules(
        ResourceRequest request,
        bool preferImportant,
        ref NetworkRule? firstNormal,
        out NetworkRule? match)
    {
        string documentHost = request.DocumentUrl?.Host ?? string.Empty;
        if (documentHost.Length == 0)
        {
            match = null;
            return false;
        }

        var lookup = _byDocumentDomain.GetAlternateLookup<ReadOnlySpan<char>>();
        ReadOnlySpan<char> host = documentHost.AsSpan();

        // news.example.com -> example.com -> com
        while (true)
        {
            if (lookup.TryGetValue(host, out List<NetworkRule>? bucket) &&
                TryScanList(bucket, request, preferImportant, ref firstNormal, out match))
            {
                return true;
            }

            int dot = host.IndexOf('.');
            if (dot < 0)
                break;

            host = host[(dot + 1)..];
        }

        if (_hasEntityDocumentDomains)
        {
            foreach (string entity in DomainMatcher.EntityKeys(documentHost))
            {
                if (_byDocumentDomain.TryGetValue(entity, out List<NetworkRule>? bucket) &&
                    TryScanList(bucket, request, preferImportant, ref firstNormal, out match))
                {
                    return true;
                }
            }
        }

        match = null;
        return false;
    }

    public void Compact()
    {
        RebalanceKeyIndex();

        _hostPrimary.TrimExcess();
        _keyPrimary.TrimExcess();
        _fallback.TrimExcess();
        _byDocumentDomain.TrimExcess();
        foreach (List<NetworkRule> bucket in _byDocumentDomain.Values)
            bucket.TrimExcess();

        if (_hostOverflow is not null)
        {
            foreach (List<NetworkRule> bucket in _hostOverflow.Values)
                bucket.TrimExcess();

            _hostOverflow.TrimExcess();
        }

        if (_keyOverflow is not null)
        {
            foreach (List<NetworkRule> bucket in _keyOverflow.Values)
                bucket.TrimExcess();

            _keyOverflow.TrimExcess();
        }
    }

    private bool TryScanHostRules(
        ResourceRequest request,
        bool preferImportant,
        ref NetworkRule? firstNormal,
        out NetworkRule? match)
    {
        ReadOnlySpan<char> host = request.Url.Host.AsSpan();

        if (TryScanHostKey(
                HashHost(host),
                request,
                preferImportant,
                ref firstNormal,
                out match))
        {
            return true;
        }

        for (int i = 0; i < host.Length; i++)
        {
            if (host[i] != '.' || i + 1 >= host.Length)
                continue;

            if (TryScanHostKey(
                    HashHost(host[(i + 1)..]),
                    request,
                    preferImportant,
                    ref firstNormal,
                    out match))
            {
                return true;
            }
        }

        match = null;
        return false;
    }

    private bool TryScanHostKey(
        ulong hostKey,
        ResourceRequest request,
        bool preferImportant,
        ref NetworkRule? firstNormal,
        out NetworkRule? match)
    {
        if (_hostPrimary.TryGetValue(hostKey, out NetworkRule? primary) &&
            TryConsider(primary, request, preferImportant, ref firstNormal, out match))
        {
            return true;
        }

        if (_hostOverflow is not null &&
            _hostOverflow.TryGetValue(hostKey, out List<NetworkRule>? overflow) &&
            TryScanList(overflow, request, preferImportant, ref firstNormal, out match))
        {
            return true;
        }

        match = null;
        return false;
    }

    private bool TryScanIndexedRules(
        ResourceRequest request,
        bool preferImportant,
        ref NetworkRule? firstNormal,
        out NetworkRule? match)
    {
        ReadOnlySpan<char> url = request.Url.AbsoluteUri.AsSpan();
        const int tokenLength = 5;

        if (url.Length < tokenLength)
        {
            match = null;
            return false;
        }

        ulong previousKey = 0;
        bool hadPreviousKey = false;

        for (int i = 0; i <= url.Length - tokenLength; i++)
        {
            if (!AbpPattern.TryPackUrlKey(url[i..], out ulong key))
                continue;

            if (hadPreviousKey && key == previousKey)
                continue;

            previousKey = key;
            hadPreviousKey = true;

            if (_keyPrimary.TryGetValue(key, out NetworkRule? primary) &&
                TryConsider(primary, request, preferImportant, ref firstNormal, out match))
            {
                return true;
            }

            if (_keyOverflow is not null &&
                _keyOverflow.TryGetValue(key, out List<NetworkRule>? overflow) &&
                TryScanList(overflow, request, preferImportant, ref firstNormal, out match))
            {
                return true;
            }
        }

        match = null;
        return false;
    }

    private static bool TryScanList(
        List<NetworkRule> rules,
        ResourceRequest request,
        bool preferImportant,
        ref NetworkRule? firstNormal,
        out NetworkRule? match)
    {
        for (int i = 0; i < rules.Count; i++)
        {
            if (TryConsider(rules[i], request, preferImportant, ref firstNormal, out match))
                return true;
        }

        match = null;
        return false;
    }

    private static bool TryConsider(
        NetworkRule rule,
        ResourceRequest request,
        bool preferImportant,
        ref NetworkRule? firstNormal,
        out NetworkRule? match)
    {
        if (!rule.Matches(request))
        {
            match = null;
            return false;
        }

        if (!preferImportant || rule.Options.Important)
        {
            match = rule;
            return true;
        }

        firstNormal ??= rule;
        match = null;
        return false;
    }

    private void AddHostRule(string host, NetworkRule rule)
    {
        ulong hostKey = HashHost(host.AsSpan());
        if (_hostPrimary.TryAdd(hostKey, rule))
            return;

        _hostOverflow ??= [];

        if (!_hostOverflow.TryGetValue(hostKey, out List<NetworkRule>? bucket))
        {
            bucket = [];
            _hostOverflow[hostKey] = bucket;
        }

        bucket.Add(rule);
    }

    private static ulong HashHost(ReadOnlySpan<char> host)
    {
        const ulong offset = 14695981039346656037UL;
        const ulong prime = 1099511628211UL;

        ulong hash = offset;
        for (int i = 0; i < host.Length; i++)
        {
            char c = host[i];
            if (c is >= 'A' and <= 'Z')
                c = (char)(c + ('a' - 'A'));

            hash ^= c;
            hash *= prime;
        }

        hash ^= (ulong)host.Length;
        hash *= prime;
        return hash;
    }

    private void RebalanceKeyIndex()
    {
        var rules = new List<NetworkRule>(_keyPrimary.Count);
        rules.AddRange(_keyPrimary.Values);
        if (_keyOverflow is not null)
        {
            foreach (List<NetworkRule> bucket in _keyOverflow.Values)
                rules.AddRange(bucket);
        }

        if (rules.Count < 2)
            return;

        var candidates = new ulong[rules.Count][];
        var frequency = new Dictionary<ulong, int>();
        var scratch = new List<ulong>();

        for (int i = 0; i < rules.Count; i++)
        {
            rules[i].Pattern.CollectIndexKeys(scratch);
            candidates[i] = scratch.ToArray();

            foreach (ulong key in candidates[i])
                frequency[key] = frequency.GetValueOrDefault(key) + 1;
        }

        _keyPrimary.Clear();
        _keyOverflow = null;

        for (int i = 0; i < rules.Count; i++)
        {
            ulong best = 0;
            int bestCount = int.MaxValue;

            foreach (ulong key in candidates[i])
            {
                int count = frequency[key];
                if (count < bestCount)
                {
                    best = key;
                    bestCount = count;
                }
            }

            if (best == 0 && !rules[i].Pattern.TryGetIndexKey(out best))
            {
                _fallback.Add(rules[i]);
                continue;
            }

            AddKeyRule(best, rules[i]);
        }
    }

    private void AddKeyRule(ulong key, NetworkRule rule)
    {
        if (_keyPrimary.TryAdd(key, rule))
            return;

        _keyOverflow ??= [];

        if (!_keyOverflow.TryGetValue(key, out List<NetworkRule>? bucket))
        {
            bucket = [];
            _keyOverflow[key] = bucket;
        }

        bucket.Add(rule);
    }
}
