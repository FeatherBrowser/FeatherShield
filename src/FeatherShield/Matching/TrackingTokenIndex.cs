using FeatherShield.Rules;

namespace FeatherShield.Matching;

internal sealed class TrackingTokenIndex
{
    private const int TokenLength = 5;

    private readonly Dictionary<ulong, string> _primary = [];
    private Dictionary<ulong, List<string>>? _overflow;
    private readonly List<string> _fallback = [];

    public void Add(string token)
    {
        if (TryGetKey(token, out ulong key))
        {
            if (_primary.TryAdd(key, token))
                return;

            _overflow ??= [];
            if (!_overflow.TryGetValue(key, out List<string>? bucket))
            {
                bucket = [];
                _overflow[key] = bucket;
            }

            bucket.Add(token);
            return;
        }

        _fallback.Add(token);
    }

    public string? Find(string url)
    {
        ReadOnlySpan<char> span = url.AsSpan();

        for (int i = 0; i <= span.Length - TokenLength; i++)
        {
            if (!AbpPattern.TryPackUrlKey(span[i..], out ulong key))
                continue;

            if (_primary.TryGetValue(key, out string? primary) &&
                url.Contains(primary, StringComparison.OrdinalIgnoreCase))
            {
                return primary;
            }

            if (_overflow is not null &&
                _overflow.TryGetValue(key, out List<string>? bucket))
            {
                for (int j = 0; j < bucket.Count; j++)
                {
                    string token = bucket[j];
                    if (url.Contains(token, StringComparison.OrdinalIgnoreCase))
                        return token;
                }
            }
        }

        for (int i = 0; i < _fallback.Count; i++)
        {
            string token = _fallback[i];
            if (url.Contains(token, StringComparison.OrdinalIgnoreCase))
                return token;
        }

        return null;
    }

    public void Compact()
    {
        _primary.TrimExcess();
        _fallback.TrimExcess();

        if (_overflow is null)
            return;

        foreach (List<string> bucket in _overflow.Values)
            bucket.TrimExcess();

        _overflow.TrimExcess();
    }

    private static bool TryGetKey(string token, out ulong key)
    {
        ReadOnlySpan<char> value = token.AsSpan();
        int bestStart = -1;
        int bestLength = 0;
        int currentStart = -1;
        int currentLength = 0;

        for (int i = 0; i <= value.Length; i++)
        {
            bool valid = i < value.Length && IsAsciiLetterOrDigit(value[i]);

            if (valid)
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

        if (bestLength < TokenLength)
        {
            key = 0;
            return false;
        }

        return AbpPattern.TryPackUrlKey(value[bestStart..], out key);
    }

    private static bool IsAsciiLetterOrDigit(char value) =>
        value is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9';
}
