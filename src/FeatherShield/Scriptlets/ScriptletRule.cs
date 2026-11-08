using FeatherShield.Matching;

namespace FeatherShield.Scriptlets;

internal sealed record ScriptletCall(string Name, string[] Args)
{
    public string Signature { get; } = BuildSignature(Name, Args);

    internal static string BuildSignature(string name, string[] args) =>
        args.Length == 0 ? name : name + "\u0001" + string.Join('\u0001', args);
}

internal sealed class ScriptletRule
{
    public ScriptletRule(
        ScriptletCall call,
        bool isException,
        string[]? includedDomains,
        string[]? excludedDomains)
    {
        Call = call;
        IsException = isException;
        IncludedDomains = includedDomains;
        ExcludedDomains = excludedDomains;
    }

    public ScriptletCall Call { get; }
    public bool IsException { get; }
    public string[]? IncludedDomains { get; }
    public string[]? ExcludedDomains { get; }
    public bool DisablesAll => IsException && Call.Name.Length == 0;

    public bool AppliesTo(string host)
    {
        if (IncludedDomains is { Length: > 0 } && !MatchesAny(host, IncludedDomains))
            return false;

        return ExcludedDomains is not { Length: > 0 } || !MatchesAny(host, ExcludedDomains);
    }

    private static bool MatchesAny(string host, string[] domains)
    {
        for (int i = 0; i < domains.Length; i++)
        {
            if (DomainMatcher.MatchesNormalized(host, domains[i]))
                return true;
        }

        return false;
    }
}
