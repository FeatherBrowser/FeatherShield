namespace FeatherShield.Cosmetic;

public static class CosmeticFilterScripts
{
    public static string Get(bool strict) => strict
        ? StrictBaseline
        : Baseline;

    private const string Baseline = """
        (() => {
          const id = "__feather_blocker_style";
          if (document.getElementById(id)) return;
          const style = document.createElement("style");
          style.id = id;
          style.textContent =
            'ins.adsbygoogle,[id^="google_ads_"],[id^="div-gpt-ad"],' +
            '[data-ad],[data-ad-slot],iframe[src*="doubleclick.net"],' +
            'iframe[src*="googlesyndication.com"]{display:none!important;}';
          (document.head || document.documentElement).appendChild(style);
        })();
        """;

    private const string StrictBaseline = """
        (() => {
          const id = "__feather_blocker_style";
          if (document.getElementById(id)) return;
          const style = document.createElement("style");
          style.id = id;
          style.textContent =
            'ins.adsbygoogle,[id^="google_ads_"],[id^="div-gpt-ad"],' +
            '[data-ad],[data-ad-slot],[aria-label="Advertisement" i],' +
            '[aria-label="Sponsored" i],[class*="sponsored" i],' +
            '[class*="promoted" i],iframe[src*="doubleclick.net"],' +
            'iframe[src*="googlesyndication.com"]{display:none!important;}';
          (document.head || document.documentElement).appendChild(style);
        })();
        """;
}
