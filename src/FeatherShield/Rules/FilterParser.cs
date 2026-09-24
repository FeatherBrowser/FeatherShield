using FeatherShield.Rules;

namespace FeatherShield;

public static class FilterParser
{
    public static void AddRule(RuleSet rules, string? raw)
    {
        ArgumentNullException.ThrowIfNull(rules);

        string line = raw?.Trim() ?? string.Empty;
        if (line.Length == 0 || line.StartsWith('#') || line.StartsWith('!'))
            return;

        bool exception = line.StartsWith("@@", StringComparison.Ordinal);
        if (exception)
            line = line[2..].Trim();

        int optionIndex = line.IndexOf('$');
        if (optionIndex >= 0)
            line = line[..optionIndex].Trim();

        if (line.StartsWith("0.0.0.0 ", StringComparison.Ordinal) ||
            line.StartsWith("127.0.0.1 ", StringComparison.Ordinal))
        {
            string[] parts = line.Split(
                ' ',
                StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            if (parts.Length >= 2)
                line = parts[1];
        }

        bool domainAnchored = line.StartsWith("||", StringComparison.Ordinal);
        if (domainAnchored)
            line = line[2..];
        else if (line.StartsWith('|'))
            line = line[1..];

        line = line.TrimEnd('|', '^').Trim();
        if (line.Length == 0)
            return;

        if (Uri.TryCreate(line, UriKind.Absolute, out Uri? absolute) &&
            !string.IsNullOrWhiteSpace(absolute.Host))
        {
            AddDomain(rules, absolute.Host.TrimStart('.'), exception);
            return;
        }

        string candidate = line.TrimStart('.');
        if (domainAnchored && TryExtractHost(candidate, out string domain))
        {
            AddDomain(rules, domain, exception);
            return;
        }

        if (LooksLikeDomain(candidate))
        {
            AddDomain(rules, candidate, exception);
            return;
        }

        if (candidate.Length >= 3)
        {
            var pattern = new UrlPattern(candidate);
            (exception ? rules.ExceptionUrlRules : rules.UrlRules).Add(pattern);
        }
    }

    public static void AddRules(RuleSet rules, IEnumerable<string> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        foreach (string line in lines)
            AddRule(rules, line);
    }

    private static void AddDomain(RuleSet rules, string domain, bool exception)
    {
        string normalized = NormalizeHost(domain);
        if (normalized.Length == 0)
            return;

        (exception ? rules.ExceptionDomains : rules.BlockedDomains).Add(normalized);
    }

    private static bool TryExtractHost(string value, out string host)
    {
        int boundary = value.IndexOfAny(['/', '^', '*', '?']);
        host = (boundary >= 0 ? value[..boundary] : value).Trim().TrimStart('.');
        return LooksLikeDomain(host);
    }

    private static bool LooksLikeDomain(string value) =>
        value.Length > 2 &&
        value.Contains('.') &&
        !value.Contains(' ') &&
        value.IndexOfAny(['/', '*', '?', '=']) < 0;

    private static string NormalizeHost(string value)
    {
        string raw = value.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            return uri.Host.TrimStart('.').TrimEnd('.');

        return raw.TrimStart('.').TrimEnd('.');
    }
}
