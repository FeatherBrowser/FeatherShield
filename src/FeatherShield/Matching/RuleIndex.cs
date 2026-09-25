using FeatherShield.Rules;

namespace FeatherShield.Matching;

internal sealed class RuleIndex
{
    private readonly Dictionary<ulong, NetworkRule> _hostPrimary = [];
    private Dictionary<ulong, List<NetworkRule>>? _hostOverflow;

    private readonly Dictionary<ulong, NetworkRule> _keyPrimary = [];
    private Dictionary<ulong, List<NetworkRule>>? _keyOverflow;

    private readonly List<NetworkRule> _fallback = [];

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

        return firstNormal;
    }

    public void Compact()
    {
        _hostPrimary.TrimExcess();
        _keyPrimary.TrimExcess();
        _fallback.TrimExcess();

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
