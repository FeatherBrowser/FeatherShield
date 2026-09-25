using System.Text.RegularExpressions;
using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class AbpPattern
{
    private const int IndexTokenLength = 5;

    private readonly Regex? _regex;
    private readonly string _pattern;
    private readonly string? _hostAnchor;
    private readonly bool _anchorStart;
    private readonly bool _anchorEnd;
    private readonly bool _matchCase;
    private readonly bool _matchAllAfterHost;
    private readonly bool _hasWildcards;
    private readonly bool _invalidRegex;

    public AbpPattern(string source, bool matchCase)
    {
        _matchCase = matchCase;

        string pattern = source.Trim();
        _hostAnchor = ExtractHostAnchor(pattern);

        if (_hostAnchor is not null)
        {
            pattern = GetHostTail(pattern);

            if (pattern.Length == 0 || pattern == "^")
            {
                _pattern = string.Empty;
                _matchAllAfterHost = true;
                return;
            }

            if (pattern[0] == '^')
                pattern = pattern[1..];
        }

        if (_hostAnchor is null && IsRegexRule(pattern))
        {
            _regex = CreateRegex(pattern[1..^1], matchCase);
            _invalidRegex = _regex is null;
            _pattern = string.Empty;
            return;
        }

        _anchorStart = _hostAnchor is null && pattern.StartsWith('|');
        if (_anchorStart)
            pattern = pattern[1..];

        _anchorEnd = pattern.EndsWith('|');
        if (_anchorEnd && pattern.Length > 0)
            pattern = pattern[..^1];

        _pattern = pattern;
        _hasWildcards = pattern.IndexOfAny(['*', '^']) >= 0;
    }

    public string? HostAnchor => _hostAnchor;
    public bool IsValid => !_invalidRegex;

    public bool IsMatch(Uri uri)
    {
        if (_hostAnchor is not null &&
            !DomainMatcher.MatchesNormalized(uri.Host, _hostAnchor))
        {
            return false;
        }

        if (_invalidRegex)
            return false;

        if (_matchAllAfterHost)
            return true;

        string url = uri.AbsoluteUri;

        if (_regex is not null)
        {
            try
            {
                return _regex.IsMatch(url);
            }
            catch (RegexMatchTimeoutException)
            {
                return false;
            }
        }

        if (_pattern.Length == 0)
            return true;

        StringComparison comparison = _matchCase
            ? StringComparison.Ordinal
            : StringComparison.OrdinalIgnoreCase;

        if (!_hasWildcards)
        {
            if (_anchorStart && _anchorEnd)
                return url.Equals(_pattern, comparison);

            if (_anchorStart)
                return url.StartsWith(_pattern, comparison);

            if (_anchorEnd)
                return url.EndsWith(_pattern, comparison);

            return url.Contains(_pattern, comparison);
        }

        if (_anchorStart)
            return MatchFrom(url, 0, comparison, _anchorEnd);

        for (int start = 0; start <= url.Length; start++)
        {
            if (MatchFrom(url, start, comparison, _anchorEnd))
                return true;
        }

        return false;
    }

    public bool TryGetIndexKey(out ulong key)
    {
        key = 0;

        if (_hostAnchor is not null || _regex is not null)
            return false;

        ReadOnlySpan<char> pattern = _pattern.AsSpan();
        int bestStart = -1;
        int bestLength = 0;
        int currentStart = -1;
        int currentLength = 0;

        for (int i = 0; i <= pattern.Length; i++)
        {
            bool tokenChar = i < pattern.Length && IsIndexCharacter(pattern[i]);

            if (tokenChar)
            {
                if (currentStart < 0)
                    currentStart = i;

                currentLength++;
                continue;
            }

            if (currentLength > bestLength)
            {
                bestStart = currentStart;
                bestLength = currentLength;
            }

            currentStart = -1;
            currentLength = 0;
        }

        if (bestLength < IndexTokenLength)
            return false;

        key = PackIndexKey(pattern.Slice(bestStart, IndexTokenLength));
        return key != 0;
    }

    internal static bool TryPackUrlKey(ReadOnlySpan<char> value, out ulong key)
    {
        if (value.Length < IndexTokenLength)
        {
            key = 0;
            return false;
        }

        for (int i = 0; i < IndexTokenLength; i++)
        {
            if (!IsIndexCharacter(value[i]))
            {
                key = 0;
                return false;
            }
        }

        key = PackIndexKey(value[..IndexTokenLength]);
        return key != 0;
    }

    private bool MatchFrom(
        string value,
        int start,
        StringComparison comparison,
        bool requireEnd)
    {
        int valueIndex = start;
        int patternIndex = 0;
        int starPattern = -1;
        int starValue = -1;

        while (valueIndex < value.Length)
        {
            if (patternIndex == _pattern.Length)
            {
                if (!requireEnd)
                    return true;

                if (starPattern >= 0)
                {
                    patternIndex = starPattern + 1;
                    valueIndex = ++starValue;
                    continue;
                }

                return false;
            }

            if (patternIndex < _pattern.Length)
            {
                char patternChar = _pattern[patternIndex];

                if (patternChar == '*')
                {
                    starPattern = patternIndex++;
                    starValue = valueIndex;
                    continue;
                }

                if (patternChar == '^')
                {
                    if (IsSeparator(value[valueIndex]))
                    {
                        patternIndex++;
                        valueIndex++;
                        continue;
                    }
                }
                else if (CharsEqual(patternChar, value[valueIndex], comparison))
                {
                    patternIndex++;
                    valueIndex++;
                    continue;
                }
            }

            if (starPattern >= 0)
            {
                patternIndex = starPattern + 1;
                valueIndex = ++starValue;
                continue;
            }

            return false;
        }

        while (patternIndex < _pattern.Length && _pattern[patternIndex] == '*')
            patternIndex++;

        if (patternIndex < _pattern.Length && _pattern[patternIndex] == '^')
            patternIndex++;

        while (patternIndex < _pattern.Length && _pattern[patternIndex] == '*')
            patternIndex++;

        return patternIndex == _pattern.Length && (!requireEnd || valueIndex == value.Length);
    }

    private static bool CharsEqual(char left, char right, StringComparison comparison)
    {
        if (comparison == StringComparison.Ordinal)
            return left == right;

        if (left == right)
            return true;

        return char.ToUpperInvariant(left) == char.ToUpperInvariant(right);
    }

    private static bool IsSeparator(char value) =>
        !char.IsLetterOrDigit(value) && value is not '_' and not '-' and not '.' and not '%';

    private static bool IsIndexCharacter(char value) =>
        value is >= (char)0x21 and <= (char)0x7E && value is not '*' and not '^' and not '|';

    private static ulong PackIndexKey(ReadOnlySpan<char> value)
    {
        ulong key = 0;

        for (int i = 0; i < IndexTokenLength; i++)
        {
            char c = value[i];
            if (c is >= 'A' and <= 'Z')
                c = (char)(c + ('a' - 'A'));

            key |= (ulong)(byte)c << (i * 8);
        }

        return key;
    }

    private static Regex? CreateRegex(string pattern, bool matchCase)
    {
        RegexOptions options = RegexOptions.CultureInvariant;
        if (!matchCase)
            options |= RegexOptions.IgnoreCase;

        try
        {
            return new Regex(
                pattern,
                options | RegexOptions.NonBacktracking,
                TimeSpan.FromMilliseconds(50));
        }
        catch (NotSupportedException)
        {
            try
            {
                return new Regex(pattern, options, TimeSpan.FromMilliseconds(50));
            }
            catch (RegexParseException)
            {
                return null;
            }
        }
        catch (RegexParseException)
        {
            return null;
        }
    }

    private static bool IsRegexRule(string pattern) =>
        pattern.Length >= 2 && pattern[0] == '/' && pattern[^1] == '/';

    private static string GetHostTail(string pattern)
    {
        ReadOnlySpan<char> value = pattern.AsSpan(2);
        int boundary = value.IndexOfAny("^/?#*|");
        return boundary >= 0 ? value[boundary..].ToString() : string.Empty;
    }

    private static string? ExtractHostAnchor(string pattern)
    {
        if (!pattern.StartsWith("||", StringComparison.Ordinal))
            return null;

        ReadOnlySpan<char> value = pattern.AsSpan(2);
        int boundary = value.IndexOfAny("^/?#*|");
        string host = (boundary >= 0 ? value[..boundary] : value)
            .ToString()
            .Trim()
            .TrimStart('.')
            .TrimEnd('.');

        if (host.Length == 0)
            return null;

        for (int i = 0; i < host.Length; i++)
        {
            char c = host[i];
            if (!(char.IsLetterOrDigit(c) || c is '.' or '-' or '_'))
                return null;
        }

        return host.ToLowerInvariant();
    }
}
