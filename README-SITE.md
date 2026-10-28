# FactsPDF · Can I Render

Compatibility lookup built with the published Purity npm packages.
Source: `site/`. Data: `site/data/compatibility.json`.
Read `docs/compatibility-site.md` for test, deployment and update instructions.

This page describes scoped engineering support, not engine release readiness.

## Publishing

Public site: <https://koalafacts.github.io/FactsPDF/>.
Use `.github/workflows/compatibility-pages.yml` as the **only Pages publisher**.
It installs the locked dependencies, tests the model and production UI, builds
Purity/Vite, then publishes only `site/dist` after the Chromium checks pass.
The generic `static.yml` template must not publish `.` or the unbuilt `site/`
source directory over this output. Workflow changes run regression checks.

After deployment, the publisher checks the public HTML and support-data SHA-256
against the tested build and confirms the compiled JavaScript/CSS are reachable.
That deployment identity is separate from each feature's engine audit commit;
rebuilding the site does not change its 418 audited compatibility records.
