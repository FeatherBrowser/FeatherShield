using FeatherShield;

namespace FeatherShield.Tests;

public sealed class ShieldEngineTests
{
    [Fact]
    public void BlocksConfiguredDomain()
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
        Assert.Equal(BlockReason.BlockedDomain, decision.Reason);
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
    public void RemovesTrackingParameters()
    {
        var rules = new RuleSet();
        rules.AddTrackingParameter("fbclid");
        var engine = new ShieldEngine(rules);

        string cleaned = engine.CleanTopLevelUrl(
            "https://example.com/page?utm_source=test&x=1&fbclid=abc");

        Assert.Equal("https://example.com/page?x=1", cleaned);
    }

    [Fact]
    public void WildcardUrlRuleMatchesInOrder()
    {
        var rules = new RuleSet();
        FilterParser.AddRule(rules, "*/ads/*banner*");
        var engine = new ShieldEngine(rules);

        BlockDecision decision = engine.Evaluate(
            new ResourceRequest(
                new Uri("https://cdn.test/static/ads/top-banner.js"),
                new Uri("https://site.test/"),
                ResourceType.Script));

        Assert.True(decision.IsBlocked);
        Assert.Equal(BlockReason.UrlRule, decision.Reason);
    }
}
