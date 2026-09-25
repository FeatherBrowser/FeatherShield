# 2.0.0-alpha.1

- Replaced the simple domain/URL blacklist model with structured network rules.
- Added ABP-style domain anchors, wildcards, separator anchors and URL anchors.
- Added resource-type options.
- Added first/third-party rule restrictions.
- Added `$domain=` include/exclude handling.
- Added exception rule precedence.
- Added cosmetic rules and cosmetic exceptions.
- Added per-site cosmetic script generation.
- Added host-indexed candidate lookup.
- Added subscription directory loading.
- Kept compatibility shims for the current Feather Browser adapter.

# 2.0.0-alpha.2

- Replaced full generic-rule scans with compact URL-key indexing.
- Replaced allocating host-suffix lookups with 64-bit suffix hashes plus rule verification.
- Removed LINQ and sorting from the per-request evaluation path.
- Replaced per-rule resource `HashSet` instances with bitmasks and shared empty options.
- Reworked cosmetic storage so global selectors do not allocate domain sets per rule.
- Added compact tracking-token indexing.
- Added duplicate-rule suppression and post-load collection compaction.
- Fixed host-anchor separator handling for rules such as `||example.com^`.
- Kept wildcard ABP patterns out of the regex engine; only explicit `/regex/` rules use `Regex`.
- Invalid regex rules and unsupported modifiers are ignored instead of crashing or broadening rules.
- Added conservative handling for unsupported procedural cosmetic syntax.
