using System.Text.Json;
using FeatherShield.Rules;

namespace FeatherShield.Cosmetic;

public static class CosmeticFilterBuilder
{
    public static IReadOnlyList<string> GetSelectors(
        RuleSet rules,
        string? host)
    {
        ArgumentNullException.ThrowIfNull(rules);
        return rules.GetCosmeticSelectors(host);
    }

    public static string BuildInjectionScript(
        RuleSet rules,
        string? host)
    {
        IReadOnlyList<string> selectors = GetSelectors(rules, host);

        if (selectors.Count == 0)
            return "(() => {})();";

        string json = JsonSerializer.Serialize(selectors);

        return $$"""
        (() => {
          const selectors = {{json}};
          if (!selectors.length) return;

          const id = "__feather_shield_2";
          let style = document.getElementById(id);

          if (!style) {
            style = document.createElement("style");
            style.id = id;
            (document.head || document.documentElement).appendChild(style);
          }

          style.textContent = selectors
            .map(selector => `${selector}{display:none!important;visibility:hidden!important;}`)
            .join("\n");
        })();
        """;
    }
}
