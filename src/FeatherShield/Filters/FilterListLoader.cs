namespace FeatherShield.Filters;

public static class FilterListLoader
{
    public static RuleSet LoadDirectory(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var rules = new RuleSet();

        LoadDomains(Path.Combine(root, "filters", "network-domains.txt"), rules.BlockedDomains);
        LoadDomains(Path.Combine(root, "exceptions", "network-domains.txt"), rules.ExceptionDomains);
        LoadValues(Path.Combine(root, "filters", "tracking-tokens.txt"), rules.TrackingTokens);
        LoadValues(Path.Combine(root, "tracking-parameters.txt"), rules.TrackingParameters);

        string customRules = Path.Combine(root, "filters", "custom-rules.txt");
        if (File.Exists(customRules))
            FilterParser.AddRules(rules, ReadValues(customRules));

        return rules;
    }

    public static void LoadRulesFile(string path, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);
        if (!File.Exists(path))
            return;

        FilterParser.AddRules(rules, ReadValues(path));
    }

    private static void LoadDomains(string path, HashSet<string> target)
    {
        if (!File.Exists(path))
            return;

        foreach (string value in ReadValues(path))
            target.Add(value.Trim().TrimStart('.').TrimEnd('.'));
    }

    private static void LoadValues(string path, ICollection<string> target)
    {
        if (!File.Exists(path))
            return;

        foreach (string value in ReadValues(path))
            target.Add(value);
    }

    private static IEnumerable<string> ReadValues(string path)
    {
        foreach (string raw in File.ReadLines(path))
        {
            string value = raw.Trim();
            if (value.Length == 0 || value.StartsWith('#') || value.StartsWith('!'))
                continue;

            yield return value;
        }
    }
}
