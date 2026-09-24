using FeatherShield.Rules;

namespace FeatherShield;

public sealed class RuleSet
{
    internal HashSet<string> BlockedDomains { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal HashSet<string> ExceptionDomains { get; } = new(StringComparer.OrdinalIgnoreCase);
    internal List<UrlPattern> UrlRules { get; } = [];
    internal List<UrlPattern> ExceptionUrlRules { get; } = [];
    internal List<string> TrackingTokens { get; } = [];
    internal HashSet<string> TrackingParameters { get; } = new(StringComparer.OrdinalIgnoreCase);

    public int BlockedDomainCount => BlockedDomains.Count;
    public int ExceptionDomainCount => ExceptionDomains.Count;
    public int UrlRuleCount => UrlRules.Count;
    public int TrackingTokenCount => TrackingTokens.Count;
    public int TrackingParameterCount => TrackingParameters.Count;

    public bool AddBlockedDomain(string domain) =>
        BlockedDomains.Add(NormalizeDomain(domain));

    public bool AddExceptionDomain(string domain) =>
        ExceptionDomains.Add(NormalizeDomain(domain));

    public void AddTrackingToken(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (!TrackingTokens.Contains(token, StringComparer.OrdinalIgnoreCase))
            TrackingTokens.Add(token.Trim());
    }

    public bool AddTrackingParameter(string parameter)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(parameter);
        return TrackingParameters.Add(parameter.Trim());
    }

    public void Clear()
    {
        BlockedDomains.Clear();
        ExceptionDomains.Clear();
        UrlRules.Clear();
        ExceptionUrlRules.Clear();
        TrackingTokens.Clear();
        TrackingParameters.Clear();
    }

    private static string NormalizeDomain(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);
        string raw = value.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            return uri.Host.TrimStart('.').TrimEnd('.');

        return raw.TrimStart('.').TrimEnd('.');
    }
}
