# CSS Syntax Core — Independent Review and Browser Oracle Gate

**Scope:** \`feat/css-syntax-core\` PR #7 against verified \`main\` \`8c303bda\`.
**Status:** Browser structural-differential and current regression tests **passed**; initial independent review **completed with seven P2 issues**, fixed with test-first changes; **fresh review on the final head is still required** before merge. No release/merge authorized by these checks.

## Review independence

The GitHub Codex integration performed a separately executed review after PR #7 was marked ready, on commit \`9d25f083\` (2026-10-10). This was **not** the author's self-review. Its seven P2 inline comments are available on [PR #7](https://github.com/KoalaFacts/FactsPDF/pull/7). The review's \`COMMENTED\` state is not a GitHub approval. The findings and corresponding tests/changes are:

| Independent finding | Reproducing regression | Fix |
| --- | --- | --- |
| Top-level stray semicolon wrongly disappears | \`LeadingSemicolonCannotDisappearFromQualifiedRulePrelude\` | Preserve it in invalid qualified prelude; strict renderer rejects |
| Unterminated CSS comments hidden from syntax consumer | \`UnterminatedCommentIsReportedAsCoreRecoveryDiagnostic\` | Separate lexical recovery diagnostics, propagated to AST |
| Unknown at-rules must preserve generic arbitrary block contents | \`UnknownAtRulePreservesArbitraryRawBlockWithoutBogusSyntaxError\` | \`CssAtRuleNode.RawBlock\`, parsed as generic components |
| Excluded unescaped Unicode controls accepted as names | \`ExcludedUnescapedNonAsciiCodePointIsNotAnIdentifier\` and supplementary tests | Restricted spec codepoint ranges + scalar-safe consumption |
| Declaration with a sole \`{}\` value misclassified as nested rule | \`SoleBraceBlockAfterColonIsADeclarationValue\` | Colon/brace declaration disambiguation |
| Invalid nested qualified prelude swallows following declarations | \`InvalidNestedRuleEndsAtSemicolonLeavingLaterDeclarations\` | Stop recovery at top-level semicolon |
| Mismatched closer inside another component discarded | \`MismatchedCloserInsideAnotherComponentBlockIsPreserved\` | Retain as ordinary component token |

The prior independent browser oracle uncovered another recovery defect: \`!invalid; color:red\` and four sibling cases lost the valid declaration. Six targeted NUnit tests (\`CssBrowserRecoveryTests\`) now guard semicolon recovery. The author observed a real red baseline before fixes: [CI 38020297217](https://github.com/KoalaFacts/FactsPDF/actions/runs/38020297217) reported **298/314 passed; 16 failed** (including the five browser cases and independently reviewed input classes). Correction commits include \`e526169\` and \`6f02936\`; no failed tests were disabled or marked ignored.

A new \`@codex review\` request was posted on the updated PR. Its completion/review findings must be inspected **separately** before declaring the independent-review gate closed. This quality record does not call the original commented review an approval.

## Larger live-Chrome differential corpus

[Chromium workflow](../.github/workflows/css-browser-differential.yml) compiles a test-only \`FactsPDF.SyntaxProbe\` and executes [this differential harness](../scripts/css_syntax_browser_differential.py) with actual Google Chrome, rather than fixed expectations handwritten by the implementer. This deliberately compares the narrow **syntax-structure projection** shared by CSSOM and the FactsPDF AST:

- Top-level and nested CSS rule types and structure;
- Parsed declaration *names* and terminal \`!important\` status;
- Parsing/recovery continuity for selected malformed declarations.

It does **not** compare property-value serialization, all selector semantics, computed styles, layout or PDF pixels. Browser CSSOM filters property-invalid declarations while the syntax AST preserves syntactically valid but unsupported properties; case selection and projection intentionally avoid asserting those different semantics are identical.

### Reproducible outcome

- **213/213 comparable cases passed** (0 mismatches) on Google Chrome **154.0.8037.97** on GitHub Actions Ubuntu, after a real failing run exposed five lost-declaration cases.
- **103 WPT-pattern-inspired cases** derived/adapted from selected paths in \`web-platform-tests/wpt\` at pinned SHA \`521d168d63dbd206ea7fbe0b74ddd5760e6e668e\`. **110 generated complementary cases**.
- [Browser differential GREEN run 38020480200](https://github.com/KoalaFacts/FactsPDF/actions/runs/38020480200); artifact \`11657901790\`, SHA-256 \`e6339d144ba18f96dfd3fad5cac3ce64c059cb99818b93a825ae3a4d45e58919\` verified against downloaded ZIP.
- [Browser differential RED run 38019950271](https://github.com/KoalaFacts/FactsPDF/actions/runs/38019950271): **208/213 passed**, five recovery mismatches; artifact \`11657856199\` retains the exact disagreements.
- The 12 selected WPT-inspired NUnit assertions from the earlier CI still run independently. Those are also not the full original upstream WPT harness.

Each generated test stores a \`family\`, \`context\`, unique ID, original source reference where applicable and derivation type. The report contains the browser version, per-family counts and failing inputs. This is significantly broader observable behavior coverage, **not** an overall CSS compatibility score, browser-standard certification or proof of parity with Chrome's renderer.

### Current regression and Native AOT

[CI 38020637453](https://github.com/KoalaFacts/FactsPDF/actions/runs/38020637453) for post-fix commit \`b9fb913\` successfully ran **314/314** NUnit cases on Ubuntu; its Windows, macOS and real Linux x64 Native AOT jobs also completed successfully. Independent stylesheet/inline PDF equivalence [run 38020637015](https://github.com/KoalaFacts/FactsPDF/actions/runs/38020637015), Unicode/font subset evidence [run 38020636872](https://github.com/KoalaFacts/FactsPDF/actions/runs/38020636872), and selected CSS syntax evidence [run 38020636870](https://github.com/KoalaFacts/FactsPDF/actions/runs/38020636870) all passed.

### CodeQL setup

The repository already has **GitHub CodeQL default setup enabled**. An attempt to add a second advanced CodeQL workflow failed for configuration reasons (run \`38020480287\`: *CodeQL analyses from advanced configurations cannot be processed when the default setup is enabled*), **not** because of a CodeQL finding. The duplicate workflow was reverted in \`b9fb913\`. Do not disable or alter the repository's existing CodeQL configuration just to force a second workflow. A separate check of CodeQL's existing default-setup findings is still required before production security claims.

## Review gate for integration

PR #7 remains unmerged until:
1. The independent fresh-context review on the post-fix head is complete and any new Important/Critical findings are resolved.
2. The exact final commit passes all platform, AOT, browser differential and PDF regression workflows.
3. At least the existing CodeQL default-setup results and any PR findings are reviewed, without falsely counting the incompatible advanced-workflow error as an engine vulnerability.

**Remaining scope:** Full upstream WPT testharness execution, property-value/selector/computed-style differential cases, adversarial fuzzing, larger Unicode source-map corpus, stable benchmark comparisons and human code review are not replaced by 213 selected structural cases. These stay visible future gates. No change to licensing or package publication.
