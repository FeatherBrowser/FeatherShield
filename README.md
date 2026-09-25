# FeatherShield 2.0 alpha

FeatherShield is the standalone filtering engine used by Feather Browser.

## 2.0 goals

- ABP-style network rules
- `@@` exception rules
- `||`, `|`, `^` and `*` pattern semantics
- resource options such as `$script`, `$image`, `$stylesheet`, `$font`, `$media`, `$xmlhttprequest`, `$document`
- `$third-party`, `$first-party`, `$1p`, `$3p` and negated party restrictions
- `$domain=` include/exclude restrictions
- cosmetic rules (`##`) and cosmetic exceptions (`#@#`)
- indexed network and tracking-rule lookup
- subscription-style `.txt` filter lists
- compatibility with Feather Browser's existing `ShieldEngine.Evaluate(...)` adapter

## Performance model

The request path is designed to stay small even with large subscriptions. Host-anchored rules are
looked up through compact host hashes, generic rules are placed into URL-key buckets, and only the
small candidate set is pattern-matched. Evaluation does not sort candidates or enumerate the whole
generic rule collection.

Default rule options share one object. Resource-type restrictions use bitmasks instead of per-rule
sets. Cosmetic selectors use a separate domain index so global selectors do not carry empty include
and exclude collections. Filter loading is streamed, duplicate rules are suppressed, and indexes are
compacted after directory loading. If rules are added manually in bulk instead of through
`FilterListLoader.LoadDirectory(...)`, call `RuleSet.Optimize()` after loading to trim spare capacity.

Only explicit `/regex/` filters create `Regex` objects. ABP wildcard rules use the lightweight native
matcher. Invalid regex rules and unsupported modifiers are skipped instead of crashing the engine or
silently turning a narrow rule into a broader network rule.

Advanced ABP/uBO features such as procedural cosmetic filtering, redirects, CSP modification,
`removeparam`, and full public-suffix-aware party classification are intentionally not emulated yet.
Unsupported modifiers are ignored at rule level rather than approximated with unsafe semantics.
