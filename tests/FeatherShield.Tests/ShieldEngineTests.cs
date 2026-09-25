using FeatherShield;

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
}
