namespace FeatherShield.Filters;

public static class FilterListLoader
{
    public static RuleSet LoadDirectory(string root)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);

        var rules = new RuleSet();

        LoadLegacyValues(
            Path.Combine(root, "filters", "tracking-tokens.txt"),
            rules.AddTrackingToken);

        LoadLegacyValues(
            Path.Combine(root, "tracking-parameters.txt"),
            value => rules.AddTrackingParameter(value));

        LoadLegacyDomains(
            Path.Combine(root, "filters", "network-domains.txt"),
            rules,
            exception: false);

        LoadLegacyDomains(
            Path.Combine(root, "exceptions", "network-domains.txt"),
            rules,
            exception: true);

        LoadRuleDirectory(Path.Combine(root, "subscriptions"), rules);
        LoadRuleDirectory(Path.Combine(root, "rules"), rules);

        string customRules = Path.Combine(root, "filters", "custom-rules.txt");
        LoadRulesFile(customRules, rules);

        return rules;
    }

    public static void LoadRulesFile(string path, RuleSet rules)
    {
        ArgumentNullException.ThrowIfNull(rules);

        if (!File.Exists(path))
            return;

        FilterParser.AddRules(rules, File.ReadLines(path));
    }

    private static void LoadRuleDirectory(string path, RuleSet rules)
    {
        if (!Directory.Exists(path))
            return;

        foreach (string file in Directory.EnumerateFiles(
                     path,
                     "*.txt",
                     SearchOption.AllDirectories))
        {
            LoadRulesFile(file, rules);
        }
    }

    private static void LoadLegacyDomains(
        string path,
        RuleSet rules,
        bool exception)
    {
        if (!File.Exists(path))
            return;

        foreach (string raw in File.ReadLines(path))
        {
            string value = raw.Trim();

            if (value.Length == 0 ||
                value.StartsWith('#') ||
                value.StartsWith('!'))
            {
                continue;
            }

            FilterParser.AddRule(
                rules,
                $"{(exception ? "@@" : string.Empty)}||{value}^");
        }
    }

    private static void LoadLegacyValues(
        string path,
        Action<string> add)
    {
        if (!File.Exists(path))
            return;

        foreach (string raw in File.ReadLines(path))
        {
            string value = raw.Trim();

            if (value.Length == 0 ||
                value.StartsWith('#') ||
                value.StartsWith('!'))
            {
                continue;
            }

            add(value);
        }
    }
}
