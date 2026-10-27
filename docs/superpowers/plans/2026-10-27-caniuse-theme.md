# Caniuse-style presentation plan

> Execute with superpowers:executing-plans in isolation.

Goal: feature-led compatibility browsing in the style requested by the owner.
Architecture: add a local stylesheet link with scoped presentation overrides;
keep the model, application JavaScript, dataset and dependencies unchanged.
Stack: current Purity/Vite, CSS and existing Playwright Chromium workflow.
Spec: ../specs/2026-10-27-caniuse-theme-design.md

Constraints: WIP=1; no fabricated version matrix; no data/renderer/license/font
changes; no managed-browser policy workaround or unsolicited publication.
Review: mobile long notes, status text/contrast, semantics, focus/dialog, routes.

- Observe new title/tile/mobile-fit checks fail against the existing UI in CI.
- Implement site/src/caniuse.css and its index.html stylesheet reference.
- Retain existing 19 browser tests; add 7 viewport/presentation/interaction checks.
- Run unchanged data/model/build gates and the complete browser suite; inspect
  real desktop, detail, mobile and English screenshots.
- Recheck main and active PR. Preserve the tested design branch while #69 remains
  active; do not create a second implementation PR. Require exact-head independent
  review and normal PR/main gates before any merge/deployment claim.
