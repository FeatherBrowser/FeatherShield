namespace FeatherShield;

public sealed record ResourceRequest(
    Uri Url,
    Uri? DocumentUrl,
    ResourceType Type);
