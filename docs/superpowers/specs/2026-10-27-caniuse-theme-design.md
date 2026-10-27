# Caniuse-inspired compatibility presentation

Owner request: make the existing FactsPDF compatibility site feel like caniuse.
Chosen implementation: independently authored warm dark shell, cream feature
panels, prominent search and solid green/amber/red textual status tiles. Retain
FactsPDF identity, existing Purity app, routes, filtering, exports and all
418 source-audit records unchanged. No copied caniuse code/assets/fonts.

A colour-only restyle retains the admin-table feel; a browser/version matrix
would fabricate evidence. Instead restyle actual semantic rows as feature-led
panels with a single truthful engine status, scope and known introduction.

Keep all existing tests, readable 13px+ notes, obvious keyboard focus, visible
status words, mobile panels without horizontal scrolling and scoped/unknown
version caveats. No new dependencies, framework upgrade, release, binary upload
or Pages claim. Preserve CSP and same-origin static resources.

PR #69 remains the only implementation PR and is owned by another execution.
Prepare in an isolated non-PR design branch; do not write its renderer/data/tests
or compete with its CI. A temporary branch trigger runs the existing site-only
verification; deploy stays main-only and the trigger must be removed before
integration. Local Chromium returned ERR_BLOCKED_BY_ADMINISTRATOR; no policy
bypass attempted. Real hosted tests are the visual execution authority.
