namespace FeatherShield.Rules;

public sealed class UrlPattern
{
    private readonly string[] _segments;

    public UrlPattern(string pattern)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(pattern);
        Pattern = pattern.Trim();
        _segments = Pattern
            .Split('*', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public string Pattern { get; }

    public bool IsMatch(string value)
    {
        if (_segments.Length == 0)
            return true;

        int position = 0;
        foreach (string segment in _segments)
        {
            int match = value.IndexOf(segment, position, StringComparison.OrdinalIgnoreCase);
            if (match < 0)
                return false;

            position = match + segment.Length;
        }

        if (!Pattern.StartsWith('*') &&
            !value.StartsWith(_segments[0], StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return Pattern.EndsWith('*') ||
               value.EndsWith(_segments[^1], StringComparison.OrdinalIgnoreCase);
    }

    public override string ToString() => Pattern;
}
