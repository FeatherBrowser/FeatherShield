namespace FeatherShield.Matching;

internal static class HostMatcher
{
    public static bool Matches(string host, string domain) =>
        host.Equals(domain, StringComparison.OrdinalIgnoreCase) ||
        host.EndsWith('.' + domain, StringComparison.OrdinalIgnoreCase);

    public static bool Related(string first, string second) =>
        first.Equals(second, StringComparison.OrdinalIgnoreCase) ||
        first.EndsWith('.' + second, StringComparison.OrdinalIgnoreCase) ||
        second.EndsWith('.' + first, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string value)
    {
        string raw = value.Trim();
        if (Uri.TryCreate(raw, UriKind.Absolute, out Uri? uri))
            return uri.Host.TrimStart('.').TrimEnd('.');

        return raw.TrimStart('*', '.').TrimEnd('/', '.');
    }
}
