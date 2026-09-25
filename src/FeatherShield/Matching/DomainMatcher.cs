namespace FeatherShield.Matching;

internal static class DomainMatcher
{
    public static bool Matches(string host, string domain)
    {
        if (string.IsNullOrWhiteSpace(host) || string.IsNullOrWhiteSpace(domain))
            return false;

        host = Normalize(host);
        domain = Normalize(domain);

        return host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
               host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsThirdParty(string requestHost, string documentHost)
    {
        if (string.IsNullOrWhiteSpace(requestHost) ||
            string.IsNullOrWhiteSpace(documentHost))
        {
            return false;
        }

        requestHost = Normalize(requestHost);
        documentHost = Normalize(documentHost);

        return !Matches(requestHost, documentHost) &&
               !Matches(documentHost, requestHost);
    }

    public static IEnumerable<string> EnumerateSuffixes(string host)
    {
        host = Normalize(host);
        if (host.Length == 0)
            yield break;

        yield return host;

        int offset = 0;
        while ((offset = host.IndexOf('.', offset)) >= 0)
        {
            offset++;
            if (offset < host.Length)
                yield return host[offset..];
        }
    }

    public static string Normalize(string value)
    {
        string raw = value.Trim();

        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            return uri.Host.TrimStart('.').TrimEnd('.').ToLowerInvariant();

        return raw
            .TrimStart('*', '.')
            .TrimEnd('/', '.')
            .ToLowerInvariant();
    }
}
