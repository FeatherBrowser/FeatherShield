# FeatherShield

FeatherShield is the standalone network and cosmetic blocking engine extracted from Feather Browser 2.0.

The library deliberately has **no dependency on WPF, WebView2 or Feather Browser settings**. Browser hosts translate their own request/context types into FeatherShield's small public model.

## Features

- Domain blocking and exception rules.
- Lightweight ABP-style domain/URL rule parsing.
- `*` wildcard matching for URL rules.
- Third-party tracker heuristics.
- Tracking-query-parameter cleanup.
- Allowlisted sites.
- Embedded normal and strict cosmetic-filter scripts.
- Loader for the separate `FeatherFilters` repository layout.

## Usage

```csharp
using FeatherShield;
using FeatherShield.Filters;

RuleSet rules = FilterListLoader.LoadDirectory(@"C:\filters\FeatherFilters");
var shield = new ShieldEngine(rules);

var request = new ResourceRequest(
    new Uri("https://ads.example.net/banner.js"),
    new Uri("https://example.com/"),
    ResourceType.Script);

BlockDecision decision = shield.Evaluate(request, new ShieldOptions());

if (decision.IsBlocked)
{
    // Return a 204/blocked response from your browser host.
}
```

## WebView2 integration

Keep the WebView2 dependency in the Browser repository. Map `CoreWebView2WebResourceContext` to `ResourceType`, then pass a `ResourceRequest` to FeatherShield.

## Building

```bash
dotnet build src/FeatherShield/FeatherShield.csproj
dotnet test tests/FeatherShield.Tests/FeatherShield.Tests.csproj
```

## License

MIT.
