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

        if (normalizedDomain.Length > 2 && normalizedDomain.EndsWith(".*", StringComparison.Ordinal))
            return MatchesEntity(host, normalizedDomain.AsSpan(0, normalizedDomain.Length - 2));

        int difference = host.Length - normalizedDomain.Length;
        return difference > 1 &&
               host[difference - 1] == '.' &&
               host.AsSpan(difference).Equals(
                   normalizedDomain.AsSpan(),
                   StringComparison.OrdinalIgnoreCase);
    }

    private const int MaxSecondLevelSuffix = 3;

    private static bool MatchesEntity(string host, ReadOnlySpan<char> name)
    {
        ReadOnlySpan<char> rest = host.AsSpan();

        for (int suffixLabels = 1; suffixLabels <= 2; suffixLabels++)
        {
            int dot = rest.LastIndexOf('.');
            if (dot <= 0 || (suffixLabels == 2 && rest.Length - dot - 1 > MaxSecondLevelSuffix))
                return false;

            rest = rest[..dot];

            if (rest.Equals(name, StringComparison.OrdinalIgnoreCase))
                return true;

            int difference = rest.Length - name.Length;
            if (difference > 1 &&
                rest[difference - 1] == '.' &&
                rest[difference..].Equals(name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static IEnumerable<string> EntityKeys(string host)
    {
        int end = host.Length;
        for (int suffixLabels = 1; suffixLabels <= 2; suffixLabels++)
        {
            int dot = host.LastIndexOf('.', end - 1);
            if (dot <= 0 || (suffixLabels == 2 && end - dot - 1 > MaxSecondLevelSuffix))
                yield break;

            end = dot;
            string rest = host[..end];

            int start = 0;
            while (start < rest.Length)
            {
                yield return rest[start..] + ".*";
                int next = rest.IndexOf('.', start);
                if (next < 0)
                    break;

                start = next + 1;
            }
        }
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
