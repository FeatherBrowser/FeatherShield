namespace FeatherShield.Matching;

internal static class DomainMatcher
{
    public static bool Matches(string host, string domain)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(domain))
            return false;

        string normalizedDomain = Normalize(domain);
        return normalizedDomain.Length > 0 && MatchesNormalized(host, normalizedDomain);
    }

    public static bool MatchesNormalized(string host, string normalizedDomain)
    {
        if (host.Length == 0 || normalizedDomain.Length == 0)
            return false;

        if (host.Equals(normalizedDomain, StringComparison.OrdinalIgnoreCase))
            return true;

        int difference = host.Length - normalizedDomain.Length;
        return difference > 1 &&
               host[difference - 1] == '.' &&
               host.AsSpan(difference).Equals(
                   normalizedDomain.AsSpan(),
                   StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsThirdParty(string requestHost, string documentHost)
    {
        if (requestHost.Length == 0 || documentHost.Length == 0)
            return false;

        return !MatchesNormalized(requestHost, documentHost) &&
               !MatchesNormalized(documentHost, requestHost);
    }

    public static string Normalize(string value)
    {
        string raw = value.Trim();

        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            raw = uri.Host;

        return raw
            .TrimStart('*', '.')
            .TrimEnd('/', '.')
            .ToLowerInvariant();
    }
}
