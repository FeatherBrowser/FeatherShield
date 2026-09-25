using System.Text;
using System.Text.RegularExpressions;
using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class AbpPattern
{
    private readonly Regex? _regex;
    private readonly string? _hostAnchor;

    public AbpPattern(string source, bool matchCase)
    {
        Source = source;
        _hostAnchor = ExtractHostAnchor(source);

        string pattern = source;

        if (_hostAnchor is not null)
        {
            pattern = pattern[2..];
            int boundary = pattern.IndexOfAny(['^', '/', '?', '#']);
            pattern = boundary >= 0 ? pattern[boundary..] : string.Empty;
        }

        if (pattern.Length > 1 &&
            pattern.StartsWith('/') &&
            pattern.EndsWith('/'))
        {
            _regex = new Regex(
                pattern[1..^1],
                RegexOptions.Compiled |
                (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase),
                TimeSpan.FromMilliseconds(100));
            return;
        }

        if (pattern.Length > 0)
        {
            _regex = BuildRegex(pattern, matchCase);
        }
    }

    public string Source { get; }

    public string? HostAnchor => _hostAnchor;

    public bool IsMatch(Uri uri)
    {
        if (_hostAnchor is not null &&
            !DomainMatcher.Matches(uri.Host, _hostAnchor))
        {
            return false;
        }

        return _regex is null || _regex.IsMatch(uri.AbsoluteUri);
    }

    private static string? ExtractHostAnchor(string pattern)
    {
        if (!pattern.StartsWith("||", StringComparison.Ordinal))
            return null;

        string value = pattern[2..];
        int boundary = value.IndexOfAny(['^', '/', '?', '#', '*']);
        string host = (boundary >= 0 ? value[..boundary] : value)
            .Trim()
            .TrimStart('.')
            .TrimEnd('.');

        return host.Contains('.') ? host : null;
    }

    private static Regex BuildRegex(string pattern, bool matchCase)
    {
        bool anchorStart = pattern.StartsWith('|');
        bool anchorEnd = pattern.EndsWith('|');

        if (anchorStart)
            pattern = pattern[1..];

        if (anchorEnd && pattern.Length > 0)
            pattern = pattern[..^1];

        var regex = new StringBuilder();

        if (anchorStart)
            regex.Append('^');

        foreach (char c in pattern)
        {
            switch (c)
            {
                case '*':
                    regex.Append(".*");
                    break;
                case '^':
                    regex.Append("(?:[^A-Za-z0-9_\\-.%]|$)");
                    break;
                default:
                    regex.Append(Regex.Escape(c.ToString()));
                    break;
            }
        }

        if (anchorEnd)
            regex.Append('$');

        return new Regex(
            regex.ToString(),
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant |
            (matchCase ? RegexOptions.None : RegexOptions.IgnoreCase),
            TimeSpan.FromMilliseconds(100));
    }
}
