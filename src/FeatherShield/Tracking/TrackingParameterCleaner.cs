namespace FeatherShield.Tracking;

internal static class TrackingParameterCleaner
{
    public static string Clean(string address, IReadOnlySet<string> parameters)
    {
        if (!Uri.TryCreate(address, UriKind.Absolute, out Uri? uri) ||
            (uri.Scheme != Uri.UriSchemeHttp &&
             uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Query))
        {
            return address;
        }

        var kept = new List<string>();
        bool changed = false;

        foreach (string part in uri.Query.TrimStart('?')
                     .Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            int equals = part.IndexOf('=');
            string rawName = equals >= 0 ? part[..equals] : part;

            string name;
            try
            {
                name = Uri.UnescapeDataString(rawName.Replace('+', ' '));
            }
            catch (UriFormatException)
            {
                name = rawName;
            }

            bool tracking =
                name.StartsWith("utm_", StringComparison.OrdinalIgnoreCase) ||
                parameters.Contains(name);

            if (tracking)
                changed = true;
            else
                kept.Add(part);
        }

        if (!changed)
            return address;

        return new UriBuilder(uri)
        {
            Query = string.Join("&", kept)
        }.Uri.AbsoluteUri;
    }
}
