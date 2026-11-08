using FeatherShield.Matching;
using FeatherShield.Rules;
using FeatherShield.Scriptlets;

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

        if (ScriptletParser.TryAdd(rules, line))
            return;

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
            out bool unsupportedModifier,
            out string? redirect,
            out bool redirectOnly);

        if (badFilter || unsupportedModifier || string.IsNullOrWhiteSpace(pattern))
            return;

        string trimmedPattern = pattern.Trim();
        if (trimmedPattern == "*")
            trimmedPattern = string.Empty;

        var compiledPattern = new AbpPattern(trimmedPattern, options.MatchCase);
        if (!compiledPattern.IsValid)
            return;

        string? resource = redirect is null ? null : RedirectResources.Normalize(redirect);

        if (exception && redirect is not null)
        {
            rules.AddRedirectException(new NetworkRule
            {
                Source = raw?.Trim() ?? line,
                IsException = true,
                Pattern = compiledPattern,
                Options = options,
                Redirect = resource ?? RedirectResources.None
            });
            return;
        }

        bool redirects = resource is not null && resource != RedirectResources.None;
        if (redirectOnly && !redirects)
            return;

        var rule = new NetworkRule
        {
            Source = raw?.Trim() ?? line,
            IsException = exception,
            Pattern = compiledPattern,
            Options = options,
            Redirect = redirects ? resource : null
        };
        if (!redirectOnly)
            rules.Add(rule);

        if (redirects)
            rules.AddRedirect(rule);
    }

    public static void AddRules(RuleSet rules, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        var blocks = new Stack<(bool ParentActive, bool Condition)>();
        bool active = true;

        foreach (string line in lines)
        {
            ReadOnlySpan<char> text = line.AsSpan().Trim();

            if (text.StartsWith("!#if ", StringComparison.Ordinal))
            {
                bool condition = PreprocessorCondition(text[5..].ToString());
                blocks.Push((active, condition));
                active = active && condition;
                continue;
            }

            if (text.StartsWith("!#else", StringComparison.Ordinal))
            {
                if (blocks.TryPop(out var block))
                {
                    blocks.Push((block.ParentActive, !block.Condition));
                    active = block.ParentActive && !block.Condition;
                }

                continue;
            }

            if (text.StartsWith("!#endif", StringComparison.Ordinal))
            {
                if (blocks.TryPop(out var block))
                    active = block.ParentActive;

                continue;
            }

            if (active)
                AddRule(rules, line);
        }
    }

    private static readonly HashSet<string> PreprocessorTrue = new(StringComparer.OrdinalIgnoreCase)
    {
        "env_chromium",
        "env_legacy",
        "cap_user_stylesheet",
        "adguard_app_windows"
    };

    internal static bool PreprocessorCondition(string expression)
    {
        foreach (string alternative in expression.Split("||"))
        {
            bool all = true;
            foreach (string part in alternative.Split("&&"))
            {
                string token = part.Trim().Trim('(', ')').Trim();
                bool negated = false;
                while (token.StartsWith('!'))
                {
                    negated = !negated;
                    token = token[1..].Trim();
                }

                bool value = token.Length > 0 && PreprocessorTrue.Contains(token);
                if (value == negated)
                {
                    all = false;
                    break;
                }
            }

            if (all)
                return true;
        }

        return false;
    }

    private static int FindOptionsSeparator(string line)
    {
        if (line.Length > 2 && line.StartsWith('/'))
        {
            for (int i = 1; i < line.Length - 1; i++)
            {
                if (line[i] == '/' && line[i + 1] == '$' && line[i - 1] != '\\')
                    return i + 1;
            }

            if (line.EndsWith('/'))
                return -1;
        }

        return line.IndexOf('$');
    }

    private static RuleOptions ParseOptions(
        string? text,
        out bool badFilter,
        out bool unsupportedModifier,
        out string? redirect,
        out bool redirectOnly)
    {
        badFilter = false;
        unsupportedModifier = false;
        redirect = null;
        redirectOnly = false;
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

            if (value.StartsWith("domain=", StringComparison.OrdinalIgnoreCase) ||
                value.StartsWith("from=", StringComparison.OrdinalIgnoreCase))
            {
                ParseDomains(
                    value[(value.IndexOf('=') + 1)..],
                    ref includedDomains,
                    ref excludedDomains);
                continue;
            }

            if (value.Equals("all", StringComparison.OrdinalIgnoreCase))
                continue;

            if (TryParseRedirect(value, out string? resource, out bool onlyRedirect))
            {
                redirect = resource;
                redirectOnly = onlyRedirect;
                continue;
            }

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

    private static bool TryParseRedirect(string value, out string? resource, out bool redirectOnly)
    {
        resource = null;
        redirectOnly = false;

        if (value.StartsWith("redirect-rule=", StringComparison.OrdinalIgnoreCase))
        {
            resource = value["redirect-rule=".Length..];
            redirectOnly = true;
            return true;
        }

        if (value.StartsWith("redirect=", StringComparison.OrdinalIgnoreCase))
        {
            resource = value["redirect=".Length..];
            return true;
        }

        if (value.StartsWith("rewrite=abp-resource:", StringComparison.OrdinalIgnoreCase))
        {
            resource = value["rewrite=".Length..];
            return true;
        }

        if (value.Equals("redirect", StringComparison.OrdinalIgnoreCase) ||
            value.Equals("redirect-rule", StringComparison.OrdinalIgnoreCase))
        {
            resource = RedirectResources.None;
            redirectOnly = value.Length > "redirect".Length;
            return true;
        }

        if (value.Equals("empty", StringComparison.OrdinalIgnoreCase))
        {
            resource = "noop.txt";
            return true;
        }

        if (value.Equals("mp4", StringComparison.OrdinalIgnoreCase))
        {
            resource = "noop-1s.mp4";
            return true;
        }

        return false;
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
