using System.Text;
using FeatherShield.Matching;
using FeatherShield.Rules;

namespace FeatherShield.Scriptlets;

internal static class ScriptletParser
{
    public static bool TryAdd(RuleSet rules, string line)
    {
        if (TryAddUbo(rules, line))
            return true;

        if (TryAddAdGuard(rules, line))
            return true;

        return TryAddAbp(rules, line);
    }

    private static bool TryAddUbo(RuleSet rules, string line)
    {
        if (!TryFindMarker(line, "##+js(", "#@#+js(", out int marker, out bool exception, out int bodyStart))
            return false;

        int close = line.LastIndexOf(')');
        if (close < bodyStart)
            return true;

        List<string> parts = SplitArguments(line[bodyStart..close]);
        Register(rules, line, line[..marker], parts, exception);
        return true;
    }

    private static bool TryAddAdGuard(RuleSet rules, string line)
    {
        if (!TryFindMarker(line, "#%#//scriptlet(", "#@%#//scriptlet(", out int marker, out bool exception, out int bodyStart))
            return false;

        int close = line.LastIndexOf(')');
        if (close < bodyStart)
            return true;

        List<string> parts = SplitArguments(line[bodyStart..close]);
        Register(rules, line, line[..marker], parts, exception);
        return true;
    }

    private static bool TryAddAbp(RuleSet rules, string line)
    {
        if (!TryFindMarker(line, "#$#", "#@$#", out int marker, out bool exception, out int bodyStart))
            return false;

        string body = line[bodyStart..].Trim();

        if (body.Contains('{'))
            return true;

        foreach (List<string> command in SplitAbpCommands(body))
            Register(rules, line + "\u0002" + string.Join('\u0001', command), line[..marker], command, exception);

        return true;
    }

    private static bool TryFindMarker(
        string line,
        string ruleMarker,
        string exceptionMarker,
        out int markerIndex,
        out bool exception,
        out int bodyStart)
    {
        markerIndex = line.IndexOf(exceptionMarker, StringComparison.Ordinal);
        if (markerIndex >= 0)
        {
            exception = true;
            bodyStart = markerIndex + exceptionMarker.Length;
            return true;
        }

        markerIndex = line.IndexOf(ruleMarker, StringComparison.Ordinal);
        exception = false;
        bodyStart = markerIndex + ruleMarker.Length;
        return markerIndex >= 0;
    }

    private static void Register(
        RuleSet rules,
        string signature,
        string domainList,
        List<string> parts,
        bool exception)
    {
        string rawName = parts.Count > 0 ? parts[0] : string.Empty;
        string name;

        if (rawName.Length == 0)
        {
            if (!exception)
                return;

            name = string.Empty;
        }
        else if (ScriptletLibrary.Canonicalize(rawName) is string canonical)
        {
            name = canonical;
        }
        else
        {
            return;
        }

        int last = parts.Count - 1;
        while (last >= 1 && parts[last].Length == 0)
            last--;

        string[] args = last >= 1 ? parts.GetRange(1, last).ToArray() : [];

        ParseDomains(domainList, out string[]? included, out string[]? excluded);

        if (!exception && included is null)
            return;

        if (!rules.TryRegisterRule(signature))
            return;

        rules.AddScriptlet(new ScriptletRule(new ScriptletCall(name, args), exception, included, excluded));
    }

    private static void ParseDomains(string value, out string[]? included, out string[]? excluded)
    {
        List<string>? include = null;
        List<string>? exclude = null;

        foreach (string entry in value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            bool negated = entry.StartsWith('~');
            string domain = DomainMatcher.Normalize(negated ? entry[1..] : entry);
            if (domain.Length == 0)
                continue;

            if (negated)
                (exclude ??= []).Add(domain);
            else
                (include ??= []).Add(domain);
        }

        included = include?.ToArray();
        excluded = exclude?.ToArray();
    }

    internal static List<string> SplitArguments(string body)
    {
        var parts = new List<string>();
        var buffer = new StringBuilder();
        int i = 0;

        while (true)
        {
            while (i < body.Length && char.IsWhiteSpace(body[i]))
                i++;

            if (i < body.Length && body[i] is '\'' or '"' or '`' &&
                TryReadQuoted(body, i, out string quoted, out int next))
            {
                parts.Add(quoted);
                if (next >= body.Length)
                    break;

                i = next + 1;
                continue;
            }

            buffer.Clear();
            while (i < body.Length)
            {
                if (body[i] == '\\' && i + 1 < body.Length && body[i + 1] == ',')
                {
                    buffer.Append(',');
                    i += 2;
                    continue;
                }

                if (body[i] == ',')
                    break;

                buffer.Append(body[i]);
                i++;
            }

            parts.Add(buffer.ToString().Trim());
            if (i >= body.Length)
                break;

            i++;
        }

        return parts;
    }

    private static bool TryReadQuoted(string body, int start, out string value, out int next)
    {
        char quote = body[start];
        var buffer = new StringBuilder();
        int i = start + 1;

        while (i < body.Length)
        {
            if (body[i] == '\\' && i + 1 < body.Length && (body[i + 1] == quote || body[i + 1] == '\\'))
            {
                buffer.Append(body[i + 1]);
                i += 2;
                continue;
            }

            if (body[i] == quote)
            {
                int j = i + 1;
                while (j < body.Length && char.IsWhiteSpace(body[j]))
                    j++;

                if (j == body.Length || body[j] == ',')
                {
                    value = buffer.ToString();
                    next = j;
                    return true;
                }

                break;
            }

            buffer.Append(body[i]);
            i++;
        }

        value = string.Empty;
        next = start;
        return false;
    }

    internal static List<List<string>> SplitAbpCommands(string body)
    {
        var commands = new List<List<string>>();
        var current = new List<string>();
        var token = new StringBuilder();
        bool inQuotes = false;
        bool hasToken = false;

        void EndToken()
        {
            if (hasToken)
                current.Add(token.ToString());

            token.Clear();
            hasToken = false;
        }

        void EndCommand()
        {
            EndToken();
            if (current.Count > 0)
                commands.Add(current);

            current = [];
        }

        for (int i = 0; i < body.Length; i++)
        {
            char c = body[i];

            if (c == '\\' && i + 1 < body.Length)
            {
                char escaped = body[++i];
                token.Append(escaped switch { 'n' => '\n', 't' => '\t', 'r' => '\r', _ => escaped });
                hasToken = true;
                continue;
            }

            if (c == '\'')
            {
                inQuotes = !inQuotes;
                hasToken = true;
                continue;
            }

            if (!inQuotes && c == ';')
            {
                EndCommand();
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                EndToken();
                continue;
            }

            token.Append(c);
            hasToken = true;
        }

        EndCommand();
        return commands;
    }
}
