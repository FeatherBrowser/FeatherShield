using System.Text.RegularExpressions;
using FeatherShield.Matching;

namespace FeatherShield.Rules;

internal sealed class AbpPattern
{
    private const int IndexTokenLength = 5;

    private readonly Regex? _regex;
    private readonly string? _regexLiteral;
    private readonly string _pattern;
    private readonly string? _hostAnchor;
    private readonly bool _anchorStart;
    private readonly bool _anchorEnd;
    private readonly bool _matchCase;
    private readonly bool _matchAllAfterHost;
    private readonly bool _hasWildcards;
    private readonly bool _invalidRegex;

    private readonly bool _tailAfterSeparator;

    private readonly string? _leadLiteral;

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
            {
                pattern = pattern[1..];
                _tailAfterSeparator = true;
            }
        }

        if (_hostAnchor is null && IsRegexRule(pattern))
        {
            _regex = CreateRegex(pattern[1..^1], matchCase);
            _invalidRegex = _regex is null;
            _regexLiteral = RequiredLiteral(pattern[1..^1]);
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
        int firstWildcard = pattern.IndexOfAny(['*', '^']);
        _hasWildcards = firstWildcard >= 0;
        _leadLiteral = firstWildcard > 0 ? pattern[..firstWildcard] : null;
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
            if (_regexLiteral is not null &&
                !url.Contains(_regexLiteral, _matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

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

        if (_hostAnchor is not null && TryFindHostEnd(uri, url, out int position))
        {
            if (_tailAfterSeparator)
            {
                if (position < url.Length && !IsSeparator(url[position]))
                    return false;

                if (position < url.Length)
                    position++;
            }

            if (!_hasWildcards)
            {
                ReadOnlySpan<char> rest = url.AsSpan(position);
                return _anchorEnd
                    ? rest.Equals(_pattern, comparison)
                    : rest.StartsWith(_pattern, comparison);
            }

            return MatchFrom(url, position, comparison, _anchorEnd);
        }

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

        if (_pattern[0] == '*')
            return MatchFrom(url, 0, comparison, _anchorEnd);

        if (_leadLiteral is not null)
        {
            for (int start = url.IndexOf(_leadLiteral, comparison);
                 start >= 0;
                 start = start + 1 < url.Length ? url.IndexOf(_leadLiteral, start + 1, comparison) : -1)
            {
                if (MatchFrom(url, start, comparison, _anchorEnd))
                    return true;
            }

            return false;
        }

        for (int start = 0; start <= url.Length; start++)
        {
            if (MatchFrom(url, start, comparison, _anchorEnd))
                return true;
        }

        return false;
    }

    private string? IndexSource =>
        _hostAnchor is not null ? null :
        _regex is not null ? _regexLiteral :
        _pattern;

    public bool TryGetIndexKey(out ulong key)
    {
        key = 0;

        if (IndexSource is not string source)
            return false;

        ReadOnlySpan<char> pattern = source.AsSpan();
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

    public void CollectIndexKeys(List<ulong> keys)
    {
        keys.Clear();

        if (IndexSource is not string source)
            return;

        ReadOnlySpan<char> pattern = source.AsSpan();
        int runStart = 0;

        for (int i = 0; i <= pattern.Length; i++)
        {
            if (i < pattern.Length && IsIndexCharacter(pattern[i]))
                continue;

            for (int start = runStart; start + IndexTokenLength <= i; start++)
            {
                ulong key = PackIndexKey(pattern.Slice(start, IndexTokenLength));
                if (key != 0 && !keys.Contains(key))
                    keys.Add(key);
            }

            runStart = i + 1;
        }
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

    private static bool TryFindHostEnd(Uri uri, string url, out int position)
    {
        int start = uri.Scheme.Length + 3;
        int index = url.IndexOf(uri.Host, start, StringComparison.OrdinalIgnoreCase);
        int length = uri.Host.Length;

        if (index < 0 && uri.IdnHost is { Length: > 0 } idn)
        {
            index = url.IndexOf(idn, start, StringComparison.OrdinalIgnoreCase);
            length = idn.Length;
        }

        if (index < 0)
        {
            position = 0;
            return false;
        }

        position = index + length;
        if (position < url.Length && url[position] == ':')
        {
            position++;
            while (position < url.Length && char.IsAsciiDigit(url[position]))
                position++;
        }

        return true;
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

    internal static string? RequiredLiteral(string regex)
    {
        int depth = 0;
        bool inClass = false;

        for (int i = 0; i < regex.Length; i++)
        {
            char c = regex[i];
            if (c == '\\')
            {
                i++;
                continue;
            }

            if (inClass)
            {
                inClass = c != ']';
                continue;
            }

            switch (c)
            {
                case '[': inClass = true; break;
                case '(': depth++; break;
                case ')': depth--; break;
                case '|' when depth == 0: return null;
            }
        }

        string best = string.Empty;
        var run = new System.Text.StringBuilder();

        void Flush()
        {
            if (run.Length > best.Length)
                best = run.ToString();

            run.Clear();
        }

        depth = 0;
        inClass = false;

        for (int i = 0; i < regex.Length; i++)
        {
            char c = regex[i];

            if (inClass)
            {
                if (c == '\\')
                    i++;
                else if (c == ']')
                    inClass = false;

                continue;
            }

            if (c == '[')
            {
                Flush();
                inClass = true;
                continue;
            }

            if (c == '(')
            {
                Flush();
                depth++;
                continue;
            }

            if (c == ')')
            {
                Flush();
                depth--;
                continue;
            }

            if (depth > 0)
            {
                if (c == '\\')
                    i++;

                continue;
            }

            if (c is '?' or '*' or '{')
            {
                if (run.Length > 0)
                    run.Length--;

                Flush();

                if (c == '{')
                {
                    int close = regex.IndexOf('}', i);
                    if (close > i)
                        i = close;
                }

                continue;
            }

            if (c is '+' or '.' or '^' or '$')
            {
                Flush();
                continue;
            }

            if (c == '\\')
            {
                if (i + 1 >= regex.Length)
                    break;

                char next = regex[++i];
                if (char.IsLetterOrDigit(next))
                {
                    Flush();
                    continue;
                }

                run.Append(next);
                continue;
            }

            run.Append(c);
        }

        Flush();
        return best.Length >= 3 ? best : null;
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
