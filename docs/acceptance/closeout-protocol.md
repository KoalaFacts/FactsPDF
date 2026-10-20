# Work package A: calibrated process peaks and reference-only source transitions

Continuation baseline: PR #20 head `7041259819509a3e692a3b6b8b0db63466531b72`.
No engine, font, candidate template, original cold/warm dataset, old benchmark or license is changed by this update. There is no new work package or automatic release.

## 1. Keep historical readings; add a separately calibrated process measurement

The prior control experiment in `peak-calibration.md` established that direct Python-to-CLI wait4 high-water can retain a large launcher floor. Linux documents preservation of resource usage across exec; GNU time documents its `%M` process maximum resident set field. This does not justify subtracting a guessed floor or changing old samples.

`scripts/acceptance_peak.py` adds supplemental Linux process measurements using the existing GNU time program as a small native supervisor. The renderer is forked by that small process, rather than directly by the large Python process. This is verification-only tooling; GNU time and the native control binary are not FactsPDF runtime dependencies or distributed artifacts.

First compile `tools/acceptance-rss-control.c` and run 0/16/128 MiB explicitly touched live allocations, three times each from independent Python launchers retaining 0 or 192 MiB. Compare supervisor RSS with the control's own live post-exec `/proc/self/status` VmHWM. Require complete, unique raw rows, sensitivity to allocated sizes, and launcher-invariant medians. The 8 MiB control-comparison allowance is a method-validation guard, not a product memory budget. Missing or zero usage is never promoted as zero-memory success.

After all controls pass, run B00, B01 and B02-100 three times from each launcher size: **18 additional native PDF executions**. Their output hashes must match the current independently inspected candidate and their medians must remain launcher-invariant. Current compiled build receipts, both executable hashes, source SHA and ordered font fingerprints must match. Original 60 cold/180 warm samples remain untouched. New results are in `peak-calibration.json`, separately labelled with scope and source.

This peak still includes process loader/runtime/font/file resources, kernel accounting and a residual small-supervisor floor. It is not engine-only live memory, all-process-tree peak, a fixed-machine SLA or a universal RSS guarantee. `launcher_elapsed_ms` includes GNU time and is not interchangeable with the original cold startup metric. Do not retroactively infer that every historical constant RSS reading had the same cause.

Reproduce controls alone:

```
python3 scripts/acceptance_peak.py calibrate --work artifacts/new-peak-controls
```

The candidate workflow runs actual document calibration after the existing native/Chrome captures and reading-space checks. Only JSON reports enter its public evidence directory; control binaries and font files remain excluded.

References: https://man7.org/linux/man-pages/man2/getrusage.2.html ; https://www.gnu.org/software/time/manual/html_node/Memory-Resources.html ; https://www.kernel.org/doc/html/latest/filesystems/proc.html .

## 2. Freeze approved bytes without a circular commit-ID requirement

Default behavior remains strict: an approval from another source commit is rejected. For an actual future human-approved reference, the record may explicitly opt in with `source_transition_policy: reference-files-only-v1`. This is not an approval record and no current review has been fabricated or changed.

Under that policy, `review.source_sha` stays the immutable, actually reviewed rendering source. The new checkout identifies its own source SHA and compiled executable receipts as before. When source IDs differ, the verifier requires:

- The candidate ID equals actual git HEAD; tracked and untracked sources are clean.
- The reviewed commit is an ancestor present in local git history. Missing/shallow history fails closed; retrieve it explicitly rather than bypassing checks.
- The two committed trees are byte-identical outside `docs/acceptance/reference-reviews.json` and regular files named `native.pdf`, `text.txt`, `page-N.png` inside `docs/acceptance/references/<case-id>/`.
- Every changed allowed file is a committed non-executable regular blob. Deleted references, symlinks, scripts, submodules, engine/templates/tests/workflows and unrelated documentation are rejected.
- Existing human identity/provenance, environment, input/font hashes, stored HEAD blob checks, actual PDF/text/raster equality and independent inspection checks STILL apply.

This permits a reference-artifact/approval-record commit and its subsequent merge to carry the original reviewed bytes without pretending the user reviewed a new renderer. It does not permit any changed renderer, template, build workflow or verifier code under an old approval. Future functional changes require new current-source review, even when their PDF happens to look alike.

Operational sequence: finish implementation/review/CI -> generate final candidate -> actual human page review -> commit the approved immutable artifacts and metadata with explicit transition policy -> rebuild/verify on the metadata-only checkout -> merge/verify actual main. A future switch from bootstrap capture to default verify changes a workflow and therefore needs its own reviewed source; it is NOT smuggled through this metadata-only exception.

## 3. Actual local evidence and outstanding gates

12 new tests were written and observed failing before either module existed, then all 12 passed locally (real GNU time/native allocations and real temporary git histories, not mocks). The first local control probe from 0/192 MiB launchers reported the same GNU-time peaks for 0/16/128 MiB controls: 524,288 / 17,301,504 / 134,742,016 bytes, with closely matching live VmHWM. These are local control observations, not FactsPDF benchmark values. Exact-head Linux Native AOT, all existing acceptance tests and actual document peaks must still be verified by Actions.

The local environment for this continuation has no .NET SDK and cannot resolve GitHub for cloning. It is an isolated scratch test workspace, not a fabricated source checkout; previous .NET results are not reused as new ones. All branch writes use the existing PR branch and exact-parent guards.

This section records the pre-approval scratch-workspace observations. Subsequently PR #20 merged: all six original native baseline cases (ten pages) were human-approved, with durable artifacts and provenance in [reference-reviews.json](reference-reviews.json). That approval is bound to those original bytes/source; it does not approve new F4 combination pages. Fixed reference hardware and numerical performance budgets remain unapproved. Technical continuation authorization does not approve unseen reference pages or budgets.
