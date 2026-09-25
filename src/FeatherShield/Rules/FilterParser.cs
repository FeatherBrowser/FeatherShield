using FeatherShield.Matching;
using FeatherShield.Rules;

namespace FeatherShield;

public static class FilterParser
{
    private static readonly Dictionary<string, ResourceType> ResourceOptions =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["document"] = ResourceType.Document,
            ["subdocument"] = ResourceType.Subdocument,
            ["script"] = ResourceType.Script,
            ["image"] = ResourceType.Image,
            ["stylesheet"] = ResourceType.Stylesheet,
            ["font"] = ResourceType.Font,
            ["media"] = ResourceType.Media,
            ["xmlhttprequest"] = ResourceType.XmlHttpRequest,
            ["xhr"] = ResourceType.XmlHttpRequest,
            ["fetch"] = ResourceType.Fetch,
            ["ping"] = ResourceType.Ping,
            ["beacon"] = ResourceType.Ping,
            ["websocket"] = ResourceType.WebSocket,
            ["object"] = ResourceType.Object,
            ["object-subrequest"] = ResourceType.Object,
            ["frame"] = ResourceType.Subdocument,
            ["css"] = ResourceType.Stylesheet,
            ["doc"] = ResourceType.Document,
            ["other"] = ResourceType.Other
        };

    public static void AddRule(RuleSet rules, string? raw)
    {
        ArgumentNullException.ThrowIfNull(rules);

        string line = raw?.Trim() ?? string.Empty;
        if (line.Length == 0 || line.StartsWith('!') || line.StartsWith('['))
            return;

        if (line.StartsWith("0.0.0.0 ", StringComparison.Ordinal) ||
            line.StartsWith("127.0.0.1 ", StringComparison.Ordinal))
        {
            int separator = line.IndexOf(' ');
            string domain = line[(separator + 1)..].Trim();
            int nextSpace = domain.IndexOfAny([' ', '\t']);
            if (nextSpace >= 0)
                domain = domain[..nextSpace];

            if (domain.Length == 0 || domain == "localhost" || domain == "localhost.localdomain")
                return;

            line = $"||{domain}^";
        }

        if (IsUnsupportedCosmeticSyntax(line))
            return;

        if (!rules.TryRegisterRule(line))
            return;

        if (TryAddCosmeticRule(rules, line))
            return;

        bool exception = line.StartsWith("@@", StringComparison.Ordinal);
        if (exception)
            line = line[2..];

        string pattern = line;
        string? optionsText = null;
        int optionIndex = FindOptionsSeparator(line);

        if (optionIndex >= 0)
        {
            pattern = line[..optionIndex];
            optionsText = line[(optionIndex + 1)..];
        }

        RuleOptions options = ParseOptions(
            optionsText,
            out bool badFilter,
            out bool unsupportedModifier);

        if (badFilter || unsupportedModifier || string.IsNullOrWhiteSpace(pattern))
            return;

        var compiledPattern = new AbpPattern(pattern.Trim(), options.MatchCase);
        if (!compiledPattern.IsValid)
            return;

        var rule = new NetworkRule
        {
            Source = raw?.Trim() ?? line,
            IsException = exception,
            Pattern = compiledPattern,
            Options = options
        };

        rules.Add(rule);
    }

    public static void AddRules(RuleSet rules, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        foreach (string line in lines)
            AddRule(rules, line);
    }

    private static int FindOptionsSeparator(string line)
    {
        if (line.Length > 2 && line.StartsWith('/') && line.LastIndexOf('/') > 0)
        {
            int closing = line.LastIndexOf('/');
            return line.IndexOf('$', closing + 1);
        }

        return line.IndexOf('$');
    }

    private static RuleOptions ParseOptions(
        string? text,
        out bool badFilter,
        out bool unsupportedModifier)
    {
        badFilter = false;
        unsupportedModifier = false;
        if (string.IsNullOrWhiteSpace(text))
            return RuleOptions.Empty;

        ulong includedTypes = 0;
        ulong excludedTypes = 0;
        List<string>? includedDomains = null;
        List<string>? excludedDomains = null;
        bool? thirdParty = null;
        bool matchCase = false;
        bool important = false;

        foreach (string raw in text.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool negated = raw.StartsWith('~');
            string value = negated ? raw[1..] : raw;

            if (value.Equals("badfilter", StringComparison.OrdinalIgnoreCase))
            {
                badFilter = true;
                continue;
            }

            if (ResourceOptions.TryGetValue(value, out ResourceType type))
            {
                ulong mask = 1UL << (int)type;
                if (negated)
                    excludedTypes |= mask;
                else
                    includedTypes |= mask;
                continue;
            }

            if (value.Equals("third-party", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("3p", StringComparison.OrdinalIgnoreCase))
            {
                thirdParty = !negated;
                continue;
            }

            if (value.Equals("first-party", StringComparison.OrdinalIgnoreCase) ||
                value.Equals("1p", StringComparison.OrdinalIgnoreCase))
            {
                thirdParty = negated;
                continue;
            }

            if (value.Equals("match-case", StringComparison.OrdinalIgnoreCase))
            {
                matchCase = !negated;
                continue;
            }

            if (value.Equals("important", StringComparison.OrdinalIgnoreCase))
            {
                important = !negated;
                continue;
            }

            if (value.StartsWith("domain=", StringComparison.OrdinalIgnoreCase))
            {
                ParseDomains(
                    value["domain=".Length..],
                    ref includedDomains,
                    ref excludedDomains);
                continue;
            }

            if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
                continue;

            unsupportedModifier = true;
        }

        string[]? included = includedDomains?.ToArray();
        string[]? excluded = excludedDomains?.ToArray();

        return RuleOptions.Create(
            includedTypes,
            excludedTypes,
            included,
            excluded,
            thirdParty,
            matchCase,
            important);
    }

    private static void ParseDomains(
        string value,
        ref List<string>? includedDomains,
        ref List<string>? excludedDomains)
    {
        foreach (string entry in value.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool excluded = entry.StartsWith('~');
            string domain = DomainMatcher.Normalize(excluded ? entry[1..] : entry);
            if (domain.Length == 0)
                continue;

            if (excluded)
            {
                excludedDomains ??= [];
                excludedDomains.Add(domain);
            }
            else
            {
                includedDomains ??= [];
                includedDomains.Add(domain);
            }
        }
    }

    private static bool TryAddCosmeticRule(RuleSet rules, string line)
    {
        int markerIndex;
        bool exception;

        if ((markerIndex = line.IndexOf("#@#", StringComparison.Ordinal)) >= 0)
        {
            exception = true;
        }
        else if ((markerIndex = line.IndexOf("##", StringComparison.Ordinal)) >= 0)
        {
            exception = false;
        }
        else
        {
            return false;
        }

        string domains = line[..markerIndex].Trim();
        string selector = line[(markerIndex + (exception ? 3 : 2))..].Trim();

        if (selector.Length == 0 ||
            selector.StartsWith('+') ||
            selector.StartsWith('^') ||
            selector.Contains(":style(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":remove(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":has-text(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":matches-css(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":xpath(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":upward(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":remove-attr(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":remove-class(", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        List<string>? included = null;
        List<string>? excluded = null;

        if (domains.Length > 0)
        {
            foreach (string entry in domains.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                bool isExcluded = entry.StartsWith('~');
                string domain = DomainMatcher.Normalize(isExcluded ? entry[1..] : entry);
                if (domain.Length == 0)
                    continue;

                if (isExcluded)
                {
                    excluded ??= [];
                    excluded.Add(domain);
                }
                else
                {
                    included ??= [];
                    included.Add(domain);
                }
            }
        }

        rules.AddCosmetic(
            selector,
            exception,
            included?.ToArray(),
            excluded?.ToArray());

        return true;
    }
    private static bool IsUnsupportedCosmeticSyntax(string line) =>
        line.Contains("#?#", StringComparison.Ordinal) ||
        line.Contains("#@?#", StringComparison.Ordinal) ||
        line.Contains("#$#", StringComparison.Ordinal) ||
        line.Contains("#@$#", StringComparison.Ordinal) ||
        line.Contains("#%#", StringComparison.Ordinal) ||
        line.Contains("#@%#", StringComparison.Ordinal);

}
