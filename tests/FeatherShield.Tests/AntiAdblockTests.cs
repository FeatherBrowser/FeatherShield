using FeatherShield;
using FeatherShield.Rules;

namespace FeatherShield.Tests;

public sealed class AntiAdblockTests
{
    private static readonly Uri Page = new("https://news.example.com/article");

    private static ShieldEngine Engine(params string[] lines)
    {
        var rules = new RuleSet();
        FilterParser.AddRules(rules, lines);
        return new ShieldEngine(rules);
    }

    private static BlockDecision Script(ShieldEngine engine, string url) =>
        engine.Evaluate(new ResourceRequest(new Uri(url), Page, ResourceType.Script));

    [Fact]
    public void RedirectRuleBlocksAndNamesTheStub()
    {
        ShieldEngine engine = Engine("/ads.js$script,redirect=noop.js");

        BlockDecision decision = Script(engine, "https://news.example.com/js/ads.js");

        Assert.True(decision.IsBlocked);
        Assert.Equal("noop.js", decision.Redirect);
        Assert.True(RedirectResources.TryGet(decision.Redirect, out RedirectResource stub));
        Assert.Equal("application/javascript", stub.MimeType);
    }

    [Theory]
    [InlineData("nooptext:-1", "noop.txt")]
    [InlineData("noopjs:10", "noop.js")]
    [InlineData("1x1.gif", "1x1.gif")]
    public void RedirectNamesIgnorePrioritySuffixes(string name, string expected)
    {
        Assert.Equal(expected, RedirectResources.Normalize(name));
    }

    [Fact]
    public void ExceptionForUnknownRedirectDoesNotCancelOtherRedirects()
    {
        ShieldEngine engine = Engine(
            "/ads.js$script,redirect=noop.js",
            "@@/ads.js$script,redirect=some-unknown-resource.js");

        BlockDecision decision = Script(engine, "https://news.example.com/js/ads.js");

        Assert.True(decision.IsBlocked);
        Assert.Equal("noop.js", decision.Redirect);
    }

    [Fact]
    public void BlockedImaSdkGetsTheStandInWhileListedSitesKeepTheRealOne()
    {
        ShieldEngine engine = Engine(
            "||imasdk.googleapis.com^",
            "||imasdk.googleapis.com/js/sdkloader/ima3.js$script,redirect=google-ima.js",
            "@@||imasdk.googleapis.com/js/sdkloader/ima3.js$domain=bbc.com");

        BlockDecision decision = Script(engine, "https://imasdk.googleapis.com/js/sdkloader/ima3.js");
        Assert.True(decision.IsBlocked);
        Assert.Equal("google-ima.js", decision.Redirect);
        Assert.True(RedirectResources.TryGet("google-ima3", out RedirectResource stub));
        Assert.Equal("application/javascript", stub.MimeType);
        Assert.Contains("google.ima=ima", System.Text.Encoding.UTF8.GetString(stub.Body.Span), StringComparison.Ordinal);

        BlockDecision listed = engine.Evaluate(new ResourceRequest(
            new Uri("https://imasdk.googleapis.com/js/sdkloader/ima3.js"),
            new Uri("https://www.bbc.com/news"),
            ResourceType.Script));
        Assert.False(listed.IsBlocked);
    }

    [Fact]
    public void RedirectRuleOnlyAppliesWhenSomethingElseBlocks()
    {
        ShieldEngine onlyDirective = Engine(
            "||googlesyndication.com/pagead/js/adsbygoogle.js$script,redirect-rule=googlesyndication_adsbygoogle.js");
        Assert.False(Script(onlyDirective, "https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js").IsBlocked);

        ShieldEngine withBlock = Engine(
            "||googlesyndication.com^",
            "||googlesyndication.com/pagead/js/adsbygoogle.js$script,redirect-rule=googlesyndication_adsbygoogle.js");
        BlockDecision decision = Script(withBlock, "https://pagead2.googlesyndication.com/pagead/js/adsbygoogle.js");

        Assert.True(decision.IsBlocked);
        Assert.Equal("googlesyndication_adsbygoogle.js", decision.Redirect);
        Assert.Null(Script(withBlock, "https://pagead2.googlesyndication.com/other.js").Redirect);
    }

    [Theory]
    [InlineData("||ads.test^$script,rewrite=abp-resource:blank-js", "noop.js")]
    [InlineData("||ads.test^$script,redirect=noopjs:100", "noop.js")]
    [InlineData("||ads.test^$script,empty", "noop.txt")]
    [InlineData("||ads.test^$media,mp4", "noop-1s.mp4")]
    public void AliasesAndAbpRewritesMapToCanonicalStubs(string rule, string expected)
    {
        ShieldEngine engine = Engine(rule);
        ResourceType type = rule.Contains("$media") ? ResourceType.Media : ResourceType.Script;

        BlockDecision decision = engine.Evaluate(new ResourceRequest(new Uri("https://ads.test/x"), Page, type));

        Assert.True(decision.IsBlocked);
        Assert.Equal(expected, decision.Redirect);
    }

    [Fact]
    public void RedirectExceptionKeepsTheBlockButDropsTheStub()
    {
        ShieldEngine engine = Engine("||ads.test^$script,redirect=noop.js", "@@||ads.test^$redirect");

        BlockDecision decision = Script(engine, "https://ads.test/a.js");

        Assert.True(decision.IsBlocked);
        Assert.Null(decision.Redirect);
    }

    [Fact]
    public void UnknownStubStillBlocks()
    {
        ShieldEngine engine = Engine("||ads.test^$script,redirect=does-not-exist.js");

        BlockDecision decision = Script(engine, "https://ads.test/a.js");

        Assert.True(decision.IsBlocked);
        Assert.Null(decision.Redirect);
    }

    [Fact]
    public void WholePagesAreNeverReplacedWithStubs()
    {
        ShieldEngine engine = Engine("||ads.test^$redirect=noop.html");

        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(new Uri("https://ads.test/"), null, ResourceType.Document));

        Assert.True(decision.IsBlocked);
        Assert.Null(decision.Redirect);
    }

    [Fact]
    public void ExceptionRuleStillAllowsRedirectTargets()
    {
        ShieldEngine engine = Engine("||ads.test^$script,redirect=noop.js", "@@||ads.test/ok.js");

        Assert.False(Script(engine, "https://ads.test/ok.js").IsBlocked);
    }

    [Fact]
    public void UboScriptletAppliesToDomainAndSubdomains()
    {
        ShieldEngine engine = Engine("example.com##+js(set, adblockDetected, false)");

        string? script = engine.GetScriptletScript("https://news.example.com/");

        Assert.NotNull(script);
        Assert.Contains("[\"set-constant\",[\"adblockDetected\",\"false\"]]", script);
        Assert.Null(engine.GetScriptletScript("https://example.org/"));
        Assert.Equal(1, engine.Rules.ScriptletRuleCount);
    }

    [Fact]
    public void AdGuardScriptletKeepsQuotedCommas()
    {
        ShieldEngine engine = Engine("example.com#%#//scriptlet('prevent-setTimeout', 'adblock, detected', '500')");

        string? script = engine.GetScriptletScript("https://example.com/");

        Assert.NotNull(script);
        Assert.Contains("[\"no-setTimeout-if\",[\"adblock, detected\",\"500\"]]", script);
    }

    [Fact]
    public void AbpSnippetsSplitIntoSeparateCalls()
    {
        ShieldEngine engine = Engine("example.com#$#abort-on-property-read detectAdblock; override-property-read showWall false");

        string? script = engine.GetScriptletScript("https://example.com/");

        Assert.NotNull(script);
        Assert.Contains("[\"abort-on-property-read\",[\"detectAdblock\"]]", script);
        Assert.Contains("[\"set-constant\",[\"showWall\",\"false\"]]", script);
    }

    [Fact]
    public void AdGuardCssInjectionIsNotMistakenForASnippet()
    {
        ShieldEngine engine = Engine("example.com#$#body { overflow: auto !important; }");

        Assert.Equal(0, engine.Rules.ScriptletRuleCount);
        Assert.Equal(0, engine.Rules.CosmeticRuleCount);
    }

    [Fact]
    public void ScriptletExceptionsDisableOneOrAll()
    {
        ShieldEngine engine = Engine(
            "example.com##+js(set, a, true)",
            "example.com##+js(aopr, b)",
            "news.example.com#@#+js(set, a, true)",
            "shop.example.com#@#+js()");

        string? news = engine.GetScriptletScript("https://news.example.com/");
        Assert.NotNull(news);
        Assert.DoesNotContain("[\"set-constant\",", news);
        Assert.Contains("[\"abort-on-property-read\",[\"b\"]]", news);

        Assert.Null(engine.GetScriptletScript("https://shop.example.com/"));
        Assert.Contains("[\"set-constant\",[\"a\",\"true\"]]", engine.GetScriptletScript("https://example.com/"));
    }

    [Fact]
    public void ExcludedDomainsAreSkipped()
    {
        ShieldEngine engine = Engine("example.com,~safe.example.com##+js(nowoif)");

        Assert.NotNull(engine.GetScriptletScript("https://example.com/"));
        Assert.Null(engine.GetScriptletScript("https://safe.example.com/"));
    }

    [Theory]
    [InlineData("##+js(set, a, false)")]
    [InlineData("example.com##+js(trusted-set-constant, a, anything)")]
    [InlineData("example.com#%#window.evil = 1;")]
    public void GenericUnknownAndRawJavaScriptRulesAreIgnored(string rule)
    {
        ShieldEngine engine = Engine(rule);

        Assert.Equal(0, engine.Rules.ScriptletRuleCount);
        Assert.Null(engine.GetScriptletScript("https://example.com/"));
    }

    [Fact]
    public void ScriptletArgumentsCannotBreakOutOfTheScript()
    {
        ShieldEngine engine = Engine("example.com##+js(set, a\"]);alert(1)//, false)");

        string script = engine.GetScriptletScript("https://example.com/")!;

        Assert.DoesNotContain("a\"]);alert(1)", script);
        Assert.Contains("a\\u0022]);alert(1)//", script);
    }

    [Fact]
    public void ScriptletsOnlyRunOnWebPages()
    {
        ShieldEngine engine = Engine("example.com##+js(nowoif)");

        Assert.Null(engine.GetScriptletScript("file:///C:/example.com/index.html"));
        Assert.Null(engine.GetScriptletScript("not a url"));
    }

    [Fact]
    public void PreprocessorKeepsOnlyDesktopChromiumBlocks()
    {
        ShieldEngine engine = Engine(
            "!#if env_firefox",
            "||firefox-only.test^",
            "!#else",
            "||not-firefox.test^",
            "!#endif",
            "!#if env_chromium && !env_mobile",
            "||chromium-desktop.test^",
            "!#if cap_html_filtering",
            "||nested-off.test^",
            "!#endif",
            "!#endif",
            "!#if env_mobile || ext_ubol",
            "||mobile.test^",
            "!#endif",
            "||always.test^");

        bool Blocked(string host) => Script(engine, $"https://{host}/a.js").IsBlocked;

        Assert.False(Blocked("firefox-only.test"));
        Assert.True(Blocked("not-firefox.test"));
        Assert.True(Blocked("chromium-desktop.test"));
        Assert.False(Blocked("nested-off.test"));
        Assert.False(Blocked("mobile.test"));
        Assert.True(Blocked("always.test"));
    }

    [Theory]
    [InlineData("https://google.com/", true)]
    [InlineData("https://www.google.de/", true)]
    [InlineData("https://news.google.co.uk/", true)]
    [InlineData("https://notgoogle.com/", false)]
    [InlineData("https://google.example.org/", false)]
    public void EntityDomainsMatchAnyTopLevelDomain(string url, bool expected)
    {
        ShieldEngine engine = Engine("google.*##+js(set, adblock, false)");

        Assert.Equal(expected, engine.GetScriptletScript(url) is not null);
    }

    [Fact]
    public void EntityExclusionsAreHonouredForNetworkRules()
    {
        ShieldEngine engine = Engine("||ads.test^$script,domain=~google.*");

        Assert.False(engine.Evaluate(new ResourceRequest(
            new Uri("https://ads.test/a.js"), new Uri("https://www.google.fr/"), ResourceType.Script)).IsBlocked);
        Assert.True(Script(engine, "https://ads.test/a.js").IsBlocked);
    }

    [Theory]
    [InlineData(@"/^https?:\/\/[a-z]{8,15}\.xyz\/[a-z]{4,}\.json$/", "https://abcdefghi.xyz/data.json", true)]
    [InlineData(@"/^https?:\/\/[a-z]{8,15}\.xyz\/[a-z]{4,}\.json$/", "https://abcdefghi.xyzz.com/data.json", false)]
    [InlineData(@"/(https?:\/\/)104\.154\..{10,}/", "https://104.154.12.34/abcdefghijklmnop", true)]
    [InlineData(@"/(https?:\/\/)104\.154\..{10,}/", "https://104.155.12.34/abcdefghijklmnop", false)]
    [InlineData(@"/ads?\.js/", "https://cdn.test/ad.js", true)]
    [InlineData(@"/ads?\.js/", "https://cdn.test/ads.js", true)]
    [InlineData(@"/colou?r-banner/", "https://cdn.test/color-banner.png", true)]
    [InlineData(@"/colou?r-banner/", "https://cdn.test/colour-banner.png", true)]
    [InlineData(@"/promo-(?:box|tile)?banner/", "https://cdn.test/promo-banner.png", true)]
    [InlineData(@"/promo-(?:box|tile)?banner/", "https://cdn.test/promo-tilebanner.png", true)]
    [InlineData(@"/tracker|beacon/", "https://cdn.test/beacon.gif", true)]
    [InlineData(@"/x\d+yzw/", "https://cdn.test/x123yzw", true)]
    [InlineData(@"/x\d+yzw/", "https://cdn.test/x123yz", false)]
    [InlineData(@"/BANNER-[0-9]+/", "https://cdn.test/banner-42.png", true)]
    public void RegexPreCheckNeverChangesTheAnswer(string rule, string url, bool expected)
    {
        ShieldEngine engine = Engine(rule);

        Assert.Equal(expected, engine.Evaluate(
            new ResourceRequest(new Uri(url), Page, ResourceType.Image)).IsBlocked);
    }

    [Fact]
    public void RegexRuleOptionsHoldingTheirOwnRegexAreParsedAsOptions()
    {
        ShieldEngine engine = Engine(@"/^https:\/\/[a-z]{8,}\.com\/x/$script,header=X-Id:/^[0-9a-f]{4}$/");

        Assert.Equal(0, engine.Rules.NetworkRuleCount);

        ShieldEngine supported = Engine(@"/^https:\/\/[a-z]{8,}\.com\/x/$script,redirect=noop.js");
        BlockDecision decision = Script(supported, "https://abcdefghij.com/x");
        Assert.True(decision.IsBlocked);
        Assert.Equal("noop.js", decision.Redirect);
    }

    [Fact]
    public void ImportantRulesStillBeatExceptionsAfterTheSplit()
    {
        ShieldEngine engine = Engine("||ads.test^", "@@||ads.test^", "||ads.test/forced.js$important");

        Assert.False(Script(engine, "https://ads.test/normal.js").IsBlocked);
        Assert.True(Script(engine, "https://ads.test/forced.js").IsBlocked);
    }

    [Theory]
    [InlineData("||cdn.test/ads/*.js", "https://cdn.test/ads/banner.js", true)]
    [InlineData("||cdn.test/ads/*.js", "https://img.cdn.test/ads/banner.js", true)]
    [InlineData("||cdn.test/ads/*.js", "https://cdn.test:8443/ads/banner.js", true)]
    [InlineData("||cdn.test/ads/*.js", "https://cdn.test/app.js?next=/ads/x.js", false)]
    [InlineData("||cdn.test/ads/", "https://cdn.test/page?u=/ads/", false)]
    [InlineData("||cdn.test^ads", "https://cdn.test/ads", true)]
    [InlineData("||cdn.test^*popunder", "https://cdn.test/js/popunder.js", true)]
    [InlineData("||cdn.test/*/track", "https://cdn.test/v2/track?x=1", true)]
    public void HostRulePathsStartRightAfterTheHost(string rule, string url, bool expected)
    {
        ShieldEngine engine = Engine(rule);

        Assert.Equal(expected, Script(engine, url).IsBlocked);
    }

    [Theory]
    [InlineData("/static/*/ads.", "https://site.test/static/v1/ads.js", true)]
    [InlineData("/static/*/ads.", "https://site.test/static/v1/app.js", false)]
    [InlineData("*/pixel.gif", "https://site.test/a/pixel.gif", true)]
    [InlineData("-ad-banner^", "https://site.test/x-ad-banner/1", true)]
    public void WildcardRulesMatchAfterReindexing(string rule, string url, bool expected)
    {
        var rules = new RuleSet();
        FilterParser.AddRules(rules, [rule, "/stat-other.js", "/static/x/y.js", "/stats.gif?"]);
        rules.Optimize();

        Assert.Equal(expected, new ShieldEngine(rules).Evaluate(
            new ResourceRequest(new Uri(url), Page, ResourceType.Script)).IsBlocked);
    }

    [Fact]
    public void FromIsASynonymForDomain()
    {
        ShieldEngine engine = Engine("||ads.test^$from=example.com");

        Assert.True(Script(engine, "https://ads.test/a.js").IsBlocked);
        Assert.False(engine.Evaluate(new ResourceRequest(
            new Uri("https://ads.test/a.js"), new Uri("https://other.org/"), ResourceType.Script)).IsBlocked);
    }
}
