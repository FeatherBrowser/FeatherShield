using FeatherShield;
using FeatherShield.Rules;

namespace FeatherShield.Tests;

public sealed class ShieldEngineTests
{
    [Fact]
    public void DomainAnchoredRuleBlocksSubdomain()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||doubleclick.net^");

        var engine = new ShieldEngine(rules);

        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://ad.doubleclick.net/banner.js"),
                new Uri("https://example.com/"),
                ResourceType.Script));

        Assert.True(decision.IsBlocked);
    }

    [Fact]
    public void ExceptionRuleWins()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||example.net^");
        FilterParser.AddRule(rules, "@@||cdn.example.net^");

        var engine = new ShieldEngine(rules);

        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://cdn.example.net/app.js"),
                new Uri("https://site.test/"),
                ResourceType.Script));

        Assert.False(decision.IsBlocked);
        Assert.Equal(BlockReason.ExceptionRule, decision.Reason);
    }

    [Fact]
    public void ScriptOptionOnlyBlocksScripts()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||ads.test^$script");

        var engine = new ShieldEngine(rules);

        Assert.True(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://ads.test/ad.js"),
                new Uri("https://site.test/"),
                ResourceType.Script)).IsBlocked);

        Assert.False(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://ads.test/logo.png"),
                new Uri("https://site.test/"),
                ResourceType.Image)).IsBlocked);
    }

    [Fact]
    public void ThirdPartyOptionIsRespected()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||tracker.test^$third-party");

        var engine = new ShieldEngine(rules);

        Assert.True(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://tracker.test/pixel"),
                new Uri("https://example.test/"),
                ResourceType.Image)).IsBlocked);

        Assert.False(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://tracker.test/pixel"),
                new Uri("https://tracker.test/"),
                ResourceType.Image)).IsBlocked);
    }

    [Fact]
    public void DomainOptionRestrictsRule()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(
            rules,
            "||ads.test^$script,domain=allowed.test|~excluded.allowed.test");

        var engine = new ShieldEngine(rules);

        Assert.True(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://ads.test/a.js"),
                new Uri("https://allowed.test/"),
                ResourceType.Script)).IsBlocked);

        Assert.False(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://ads.test/a.js"),
                new Uri("https://excluded.allowed.test/"),
                ResourceType.Script)).IsBlocked);
    }

    [Fact]
    public void CosmeticExceptionRemovesSelector()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "##.advert");
        FilterParser.AddRule(rules, "example.com#@#.advert");

        var engine = new ShieldEngine(rules);

        Assert.DoesNotContain(
            ".advert",
            engine.GetCosmeticSelectors("example.com"));

        Assert.Contains(
            ".advert",
            engine.GetCosmeticSelectors("other.test"));
    }

    [Fact]
    public void RemovesTrackingParameters()
    {
        var rules = new RuleSet();
        rules.AddTrackingParameter("fbclid");

        var engine = new ShieldEngine(rules);

        string cleaned = engine.CleanTopLevelUrl(
            "https://example.com/page?utm_source=test&x=1&fbclid=abc");

        Assert.Equal(
            "https://example.com/page?x=1",
            cleaned);
    }


    [Fact]
    public void WildcardRuleDoesNotRequireRegexCompilation()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "*/ads-coordinator*");

        var engine = new ShieldEngine(rules);

        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://cdn.test/assets/ads-coordinator/v2.js?x=1"),
                new Uri("https://site.test/"),
                ResourceType.Script));

        Assert.True(decision.IsBlocked);
    }

    [Fact]
    public void InvalidRegexRuleIsIgnored()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "/[/");

        Assert.Equal(0, rules.NetworkRuleCount);

        var engine = new ShieldEngine(rules);
        Assert.False(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://example.test/file.js"),
                new Uri("https://site.test/"),
                ResourceType.Script)).IsBlocked);
    }

    [Fact]
    public void DuplicateRulesAreStoredOnce()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||doubleclick.net^");
        FilterParser.AddRule(rules, "||doubleclick.net^");

        Assert.Equal(1, rules.NetworkRuleCount);
    }

    [Fact]
    public void ImportantRuleIsPreferredWithoutSortingCandidates()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "ads-coordinator");
        FilterParser.AddRule(rules, "ads-coordinator$important");

        var engine = new ShieldEngine(rules);
        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://cdn.test/ads-coordinator.js"),
                new Uri("https://site.test/"),
                ResourceType.Script));

        Assert.True(decision.IsBlocked);
        Assert.Equal("ads-coordinator$important", decision.MatchedRule);
    }

    [Fact]
    public void CosmeticExclusionKeepsSelectorOffExcludedDomain()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "~example.com##.generic-ad");

        var engine = new ShieldEngine(rules);

        Assert.DoesNotContain(".generic-ad", engine.GetCosmeticSelectors("example.com"));
        Assert.Contains(".generic-ad", engine.GetCosmeticSelectors("other.test"));
    }

    [Fact]
    public void LargeGenericRuleSetStillFindsIndexedMatch()
    {
        var rules = new RuleSet();

        for (int i = 0; i < 10000; i++)
            FilterParser.AddRule(rules, $"unusedtoken{i:D5}");

        FilterParser.AddRule(rules, "specialtrackerasset");
        var engine = new ShieldEngine(rules);

        Assert.True(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://cdn.test/js/specialtrackerasset.js"),
                new Uri("https://site.test/"),
                ResourceType.Script)).IsBlocked);
    }


    [Fact]
    public void UnsupportedElemhideExceptionDoesNotBecomeNetworkAllowRule()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||example.test^");
        FilterParser.AddRule(rules, "@@||example.test^$elemhide");

        var engine = new ShieldEngine(rules);
        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://example.test/ad.js"),
                new Uri("https://site.test/"),
                ResourceType.Script));

        Assert.True(decision.IsBlocked);
        Assert.Equal(0, rules.ExceptionRuleCount);
    }

    [Fact]
    public void UnsupportedProceduralCosmeticRuleIsSkipped()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "example.test#?#div:has-text(Sponsored)");

        Assert.Equal(0, rules.CosmeticRuleCount);
        Assert.Equal(0, rules.NetworkRuleCount);
    }

    [Fact]
    public void FirstPartyAliasIsRespected()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "||tracker.test^$1p");

        var engine = new ShieldEngine(rules);

        Assert.True(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://tracker.test/pixel"),
                new Uri("https://tracker.test/"),
                ResourceType.Image)).IsBlocked);

        Assert.False(engine.Evaluate(
            new ResourceRequest(
                new Uri("https://tracker.test/pixel"),
                new Uri("https://other.test/"),
                ResourceType.Image)).IsBlocked);
    }


    [Fact]
    public void ImportantBlockingRuleOverridesExceptionRule()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "@@||tracker.test^");
        FilterParser.AddRule(rules, "||tracker.test^$important");

        var engine = new ShieldEngine(rules);
        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://tracker.test/pixel"),
                new Uri("https://site.test/"),
                ResourceType.Image));

        Assert.True(decision.IsBlocked);
        Assert.Equal("||tracker.test^$important", decision.MatchedRule);
    }

}
