using System.Text;
using System.Text.RegularExpressions;
using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class AbpPattern
{
    private readonly Regex? _regex;
    private readonly string? _hostAnchor;

    public AbpPattern(
        string source,
        bool matchCase)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);

        Source = source.Trim();

        _hostAnchor = ExtractHostAnchor(Source);

        string pattern = Source;

        if (_hostAnchor is not null)
        {
            pattern = pattern[2..];

            int boundary = pattern.IndexOfAny(
                ['^', '/', '?', '#', '*']);

            pattern = boundary >= 0
                ? pattern[boundary..]
                : string.Empty;
        }

        RegexOptions options =
            RegexOptions.Compiled |
            RegexOptions.CultureInvariant;

        if (!matchCase)
            options |= RegexOptions.IgnoreCase;

        if (IsRegexRule(pattern))
        {
            string regexPattern = pattern[1..^1];

            try
            {
                _regex = new Regex(
                    regexPattern,
                    options,
                    TimeSpan.FromMilliseconds(100));
            }
            catch (RegexParseException)
            {
                _regex = null;
            }

            return;
        }

        if (pattern.Length == 0)
            return;

        string converted = ConvertAbpPattern(pattern);

        if (converted.Length == 0)
            return;

        _regex = new Regex(
            converted,
            options,
            TimeSpan.FromMilliseconds(100));
    }

    public string Source { get; }

    public string? HostAnchor => _hostAnchor;

    public bool IsMatch(Uri uri)
    {
        ArgumentNullException.ThrowIfNull(uri);

        if (_hostAnchor is not null &&
            !DomainMatcher.Matches(
                uri.Host,
                _hostAnchor))
        {
            return false;
        }

        return _regex is null ||
               _regex.IsMatch(uri.AbsoluteUri);
    }

    private static bool IsRegexRule(
        string pattern)
    {
        if (pattern.Length < 2)
            return false;

        if (pattern[0] != '/' ||
            pattern[^1] != '/')
        {
            return false;
        }

        return true;
    }

    private static string ConvertAbpPattern(
        string pattern)
    {
        bool anchorStart = false;
        bool anchorEnd = false;

        if (pattern.StartsWith('|') &&
            !pattern.StartsWith("||", StringComparison.Ordinal))
        {
            anchorStart = true;
            pattern = pattern[1..];
        }

        if (pattern.EndsWith('|') &&
            pattern.Length > 0)
        {
            anchorEnd = true;
            pattern = pattern[..^1];
        }

        var builder = new StringBuilder(
            pattern.Length * 2);

        if (anchorStart)
            builder.Append('^');

        foreach (char character in pattern)
        {
            switch (character)
            {
                case '*':
                    builder.Append(".*");
                    break;

                case '^':
                    builder.Append(
                        "(?:[^A-Za-z0-9_\\-.%]|$)");
                    break;

                default:
                    builder.Append(
                        Regex.Escape(
                            character.ToString()));
                    break;
            }
        }

        if (anchorEnd)
            builder.Append('$');

        return builder.ToString();
    }

    private static string? ExtractHostAnchor(
        string pattern)
    {
        if (!pattern.StartsWith(
                "||",
                StringComparison.Ordinal))
        {
            return null;
        }

        string value = pattern[2..];

        int boundary = value.IndexOfAny(
            ['^', '/', '?', '#', '*', '|']);

        string host = (
                boundary >= 0
                    ? value[..boundary]
                    : value)
            .Trim()
            .TrimStart('.')
            .TrimEnd('.');

        if (host.Length == 0 ||
            !host.Contains('.'))
        {
            return null;
        }

        return DomainMatcher.Normalize(host);
    }
}