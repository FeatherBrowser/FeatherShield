using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class RuleOptions
{
    private const int MaxCachedOptionSets = 256;

    private static readonly object CacheLock = new();
    private static readonly Dictionary<ulong, RuleOptions> Cache = [];

    public static readonly RuleOptions Empty = new(
        includedTypes: 0,
        excludedTypes: 0,
        includedDomains: null,
        excludedDomains: null,
        thirdParty: null,
        matchCase: false,
        important: false);

    private readonly ulong _includedTypes;
    private readonly ulong _excludedTypes;
    private readonly string[]? _includedDomains;
    private readonly string[]? _excludedDomains;

    private RuleOptions(
        ulong includedTypes,
        ulong excludedTypes,
        string[]? includedDomains,
        string[]? excludedDomains,
        bool? thirdParty,
        bool matchCase,
        bool important)
    {
        _includedTypes = includedTypes;
        _excludedTypes = excludedTypes;
        _includedDomains = includedDomains;
        _excludedDomains = excludedDomains;
        ThirdParty = thirdParty;
        MatchCase = matchCase;
        Important = important;
    }

    public bool? ThirdParty { get; }
    public bool MatchCase { get; }
    public bool Important { get; }

    public static RuleOptions Create(
        ulong includedTypes,
        ulong excludedTypes,
        string[]? includedDomains,
        string[]? excludedDomains,
        bool? thirdParty,
        bool matchCase,
        bool important)
    {
        if (includedTypes == 0 &&
            excludedTypes == 0 &&
            includedDomains is null &&
            excludedDomains is null &&
            thirdParty is null &&
            !matchCase &&
            !important)
        {
            return Empty;
        }

        if (includedDomains is not null ||
            excludedDomains is not null ||
            includedTypes > ushort.MaxValue ||
            excludedTypes > ushort.MaxValue)
        {
            return new RuleOptions(
                includedTypes,
                excludedTypes,
                includedDomains,
                excludedDomains,
                thirdParty,
                matchCase,
                important);
        }

        ulong key = BuildCacheKey(
            includedTypes,
            excludedTypes,
            thirdParty,
            matchCase,
            important);

        lock (CacheLock)
        {
            if (Cache.TryGetValue(key, out RuleOptions? cached))
                return cached;

            var options = new RuleOptions(
                includedTypes,
                excludedTypes,
                null,
                null,
                thirdParty,
                matchCase,
                important);

            if (Cache.Count < MaxCachedOptionSets)
                Cache[key] = options;

            return options;
        }
    }

    public bool Matches(ResourceRequest request)
    {
        ulong type = 1UL << (int)request.Type;

        if (_includedTypes != 0 && (_includedTypes & type) == 0)
            return false;

        if ((_excludedTypes & type) != 0)
            return false;

        string documentHost = request.DocumentUrl?.Host ?? string.Empty;

        if (_includedDomains is { Length: > 0 } &&
            !MatchesAnyDomain(documentHost, _includedDomains))
        {
            return false;
        }

        if (_excludedDomains is { Length: > 0 } &&
            MatchesAnyDomain(documentHost, _excludedDomains))
        {
            return false;
        }

        if (ThirdParty is bool requiredThirdParty &&
            DomainMatcher.IsThirdParty(request.Url.Host, documentHost) != requiredThirdParty)
        {
            return false;
        }

        return true;
    }

    private static ulong BuildCacheKey(
        ulong includedTypes,
        ulong excludedTypes,
        bool? thirdParty,
        bool matchCase,
        bool important)
    {
        ulong party = thirdParty switch
        {
            false => 1UL,
            true => 2UL,
            _ => 0UL
        };

        ulong key = includedTypes;
        key |= excludedTypes << 16;
        key |= party << 32;

        if (matchCase)
            key |= 1UL << 34;

        if (important)
            key |= 1UL << 35;

        return key;
    }

    private static bool MatchesAnyDomain(string host, string[] domains)
    {
        for (int i = 0; i < domains.Length; i++)
        {
            if (DomainMatcher.MatchesNormalized(host, domains[i]))
                return true;
        }

        return false;
    }
}
