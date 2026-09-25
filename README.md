# FeatherShield 2.0 alpha

FeatherShield is the standalone filtering engine used by Feather Browser.

## 2.0 goals

- ABP-style network rules
- `@@` exception rules
- `||`, `|`, `^` and `*` pattern semantics
- resource options such as `$script`, `$image`, `$stylesheet`, `$font`, `$media`, `$xmlhttprequest`, `$document`
- `$third-party` and `~third-party`
- `$domain=` include/exclude restrictions
- cosmetic rules (`##`) and cosmetic exceptions (`#@#`)
- indexed host-anchored rule lookup
- subscription-style `.txt` filter lists
- compatibility with Feather Browser's existing `ShieldEngine.Evaluate(...)` adapter

This is an alpha implementation. Advanced ABP/uBO syntax such as procedural cosmetic filters,
redirect rules, CSP rules, removeparam rules, modifiers, and full public-suffix-aware party
classification are not yet implemented.
