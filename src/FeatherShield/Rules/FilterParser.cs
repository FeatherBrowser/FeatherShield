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
            ["ping"] = ResourceType.Ping,
            ["websocket"] = ResourceType.WebSocket,
            ["object"] = ResourceType.Object,
            ["other"] = ResourceType.Other
        };

    public static void AddRule(RuleSet rules, string? raw)
    {
        ArgumentNullException.ThrowIfNull(rules);

        string line = raw?.Trim() ?? string.Empty;

        if (line.Length == 0 ||
            line.StartsWith('!') ||
            line.StartsWith('['))
        {
            return;
        }

        if (TryAddCosmeticRule(rules, line))
            return;

        if (line.StartsWith("0.0.0.0 ", StringComparison.Ordinal) ||
            line.StartsWith("127.0.0.1 ", StringComparison.Ordinal))
        {
            string[] parts = line.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries |
                StringSplitOptions.TrimEntries);

            if (parts.Length >= 2)
                line = $"||{parts[1]}^";
        }

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

        RuleOptions options = ParseOptions(optionsText);

        if (optionsText is not null &&
            optionsText.Split(',').Any(option =>
                option.Equals("badfilter", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(pattern))
            return;

        var rule = new NetworkRule
        {
            Source = raw ?? line,
            IsException = exception,
            Pattern = new AbpPattern(pattern.Trim(), options.MatchCase),
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
        if (line.Length > 2 &&
            line.StartsWith('/') &&
            line.LastIndexOf('/') > 0)
        {
            int closing = line.LastIndexOf('/');
            int dollar = line.IndexOf('$', closing + 1);
            return dollar;
        }

        return line.IndexOf('$');
    }

    private static RuleOptions ParseOptions(string? text)
    {
        var options = new RuleOptions();

        if (string.IsNullOrWhiteSpace(text))
            return options;

        foreach (string raw in text.Split(
                     ',',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            bool negated = raw.StartsWith('~');
            string value = negated ? raw[1..] : raw;

            if (ResourceOptions.TryGetValue(value, out ResourceType type))
            {
                (negated ? options.ExcludedTypes : options.IncludedTypes).Add(type);
                continue;
            }

            if (value.Equals("third-party", StringComparison.OrdinalIgnoreCase))
            {
                options.ThirdParty = !negated;
                continue;
            }

            if (value.Equals("match-case", StringComparison.OrdinalIgnoreCase))
            {
                options.MatchCase = !negated;
                continue;
            }

            if (value.Equals("important", StringComparison.OrdinalIgnoreCase))
            {
                options.Important = !negated;
                continue;
            }

            if (value.StartsWith("domain=", StringComparison.OrdinalIgnoreCase))
            {
                ParseDomains(value["domain=".Length..], options);
            }
        }

        return options;
    }

    private static void ParseDomains(string value, RuleOptions options)
    {
        foreach (string entry in value.Split(
                     '|',
                     StringSplitOptions.RemoveEmptyEntries |
                     StringSplitOptions.TrimEntries))
        {
            bool excluded = entry.StartsWith('~');
            string domain = (excluded ? entry[1..] : entry)
                .Trim()
                .TrimStart('.')
                .TrimEnd('.');

            if (domain.Length == 0)
                continue;

            (excluded
                ? options.ExcludedDomains
                : options.IncludedDomains).Add(domain);
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
            selector.Contains(":style(", StringComparison.OrdinalIgnoreCase) ||
            selector.Contains(":remove(", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var rule = new CosmeticRule
        {
            Selector = selector,
            IsException = exception
        };

        if (domains.Length > 0)
        {
            foreach (string entry in domains.Split(
                         ',',
                         StringSplitOptions.RemoveEmptyEntries |
                         StringSplitOptions.TrimEntries))
            {
                bool excluded = entry.StartsWith('~');
                string domain = excluded ? entry[1..] : entry;

                (excluded
                    ? rule.ExcludedDomains
                    : rule.IncludedDomains).Add(domain);
            }
        }

        rules.Add(rule);
        return true;
    }
}
