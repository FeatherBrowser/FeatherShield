namespace FeatherShield;

public readonly record struct BlockDecision(
    bool IsBlocked,
    BlockReason Reason,
    string? MatchedRule = null)
{
    public static BlockDecision Allow(
        BlockReason reason = BlockReason.None,
        string? rule = null) =>
        new(false, reason, rule);

    public static BlockDecision Block(
        BlockReason reason,
        string? rule = null) =>
        new(true, reason, rule);
}
