# FactsPDF 工作包 A：真实文档验收基线 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:executing-plans，当前会话逐项执行；只有环境真正支持且用户选择时才用 subagent-driven-development。每项按测试→RED→实现→GREEN→记录推进。

**Goal:** 不改引擎，交付可复现的报告验收集、真实支持矩阵与明确范围的性能基线。
**Architecture:** 确定性合成语料和清单驱动生产 CLI、仅用公共 API 的验收宿主及独立 PDF 检查。已有能力、预期错误、未实现目标分别统计；先采候选，再独立检查/人工审阅，最后冻结。
**Tech Stack:** 仓库 .NET 10/C#、Python 标准库 unittest、Linux x64 Native AOT，验证环境的 qpdf/Poppler/Chrome/Pillow；不成为核心依赖。
**Spec:** `docs/superpowers/specs/2026-10-11-first-usable-release-scope-design.md`，工作包 A。B–G 保持路线。
**Status:** 用户已接受范围，审阅本计划后明确要求“执行”。0.2 修订只解决独立复审发现的测量/资源/入口明确性问题，不引入新的产品能力。
**Engine baseline:** M12 `1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6`。规划 PR #19 合并并核对 main 后才建立 A 的唯一实现 PR，不预占 M13。

## Global Constraints

- A 不改 `src/`、既有测试期待、旧 benchmark、许可和产品 API。只新增验收语料、公共 API 宿主、验证脚本/测试、文档与独立工作流。
- baseline、negative、target 分母独立；expected-error、skip、blocked-design 不等于功能通过。
- 真值必须来自独立检查和实际页面审阅。capture 不能批准自己，未取得用户页面审批保持 pending-review。
- 字体只记录显式来源、授权说明和指纹，不向用户分发字体文件、原生二进制或秘密。
- 旧 `.github/workflows/box-layout-resources.yml` 与 `benchmarks/FactsPDF.Benchmarks/Program.cs` 保持固定 M8 对照；A 的 M12 基线独立命名。
- A 只验证 Linux x64 实际 Native AOT，继续现有跨平台测试，不交付 Windows AOT/Node SDK/WASM/公开包。
- 未实测的页数、性能、RSS和未取得的审批不能编造。共享 CI 是 provisional，未指定固定机器和未批准数值预算要明确显示。
- 一个实现 PR 通过独立审查、合并及实际 main 验证后，才建立下一实现 PR。

## Review Focus

目标报错/跳过不可被计通过；字体/工具漂移不可自动刷新真值；缺字/重复/顺序/越界不可被可打开 PDF 掩盖；错误和超时不能破坏已有输出；原始采样不能省略、补零、改名或把累计分配当峰值。

## 待创建文件与职责

| 路径 | 职责 |
| --- | --- |
| `examples/acceptance/manifest.json` | Case 类别、要求、输入指纹、资源及逐入口合同 |
| `examples/acceptance/data/report-records.json` | 确定性中英合成记录、稳定 ID |
| `examples/acceptance/baseline/` | ASCII 控制、双语简报、记录增长、跨页面板 |
| `examples/acceptance/targets/requirements.json` | 未实现需求及隔离探针，不杜撰新 API |
| `examples/acceptance/negative/` | 各类资源边界、过宽/零宽与非法文本声明 |
| `scripts/acceptance_corpus.py` | 清单/生成/哈希/分类汇总 |
| `scripts/acceptance_run.py` | CLI/API 隔离运行、超时及原始证据 |
| `scripts/acceptance_inspect.py` | PDF/文字/逐页检查及参考验证 |
| `scripts/acceptance_measure.py` | 冷/热/RSS调度、原始采样与统计 |
| `scripts/test_acceptance_*.py` | 四模块对应 unittest，不需网络或真实字体 |
| `tools/FactsPDF.Acceptance/{FactsPDF.Acceptance.csproj,Program.cs,RenderCommand.cs,MeasureCommand.cs}` | 只调用公共 API 的独立 net10.0 宿主；ProjectReference `../../src/FactsPDF/FactsPDF.csproj`，无 PackageReference |
| `docs/acceptance/{README.md,support-matrix.md,performance-baseline.md,reference-reviews.json}` | 重跑说明、实际矩阵、测量与预算、真实审批记录 |
| `.github/workflows/document-acceptance.yml` | 独立验收工作流及白名单证据上传 |

模块可按职责合理再拆小文件，不重构引擎。产物输出 `artifacts/document-acceptance/`；参考记录绑定持久可取位置和哈希，临时 Actions 链接不能是唯一永久参考。

## 共享合同（所有任务必须一致）

`schema_version=1`。每例有唯一 `id`、`kind`（baseline/negative/target）、`requirements`、`input_path`、`records`、`font_roles`、`checks`。路径是验收目录内部相对路径；禁止绝对路径、`..` 和 symlink 逃逸。未定接口的 target 可以 null 输入且 blocked-design，仍进入目标分母。

**逐入口适用性必填：** `entrypoints` 为映射，键仅 `cli` / `api`，值包含 `expected_exit` 和可空 `expected_code`。只运行映射中列明的入口，不把不适用入口称为 skip/pass。baseline 在两个入口都是成功。N-wide、N-zero 可在两个入口期待 FPDF1302/exit 3；需要调低额度或构造无效 UTF-16 的案例只列 api。`host_limits` 只能选已存在 PdfOptions 正整数额度；`invalid_scalar` 只许高/低孤立代理项。生产 CLI 没有 `--max-pages`，不得向它传宿主参数。

结果状态：baseline=passed/regression/pending-review/infrastructure-error；negative=expected-error/unexpected-success/wrong-error/infrastructure-error；target=unsupported/blocked-design/candidate/verified/infrastructure-error。新成功只为 candidate；verified 必须有所有独立断言及真实审阅。未知/重复/缺失运行记录均不得计成功。

运行记录含 case_id、entrypoint、实际 source_sha、构建模式、输入/有序字体指纹、环境指纹、配置、退出码、结构化错误、输出字节/哈希、计时和超时/取消状态。原始结果不可被汇总覆盖。每个 Case 汇总所有适用入口，必须全部满足才能计该 Case 通过。

汇总分别列 baseline_regression_passed、negative_expected_errors、target_verified_count/target_total、evidence_complete；A 永远不发放 preview_ready=true。capture 只采候选，verify 缺有效冻结记录要失败，不能自动降级。

---

## Task 1：确定性语料与诚实状态

**Interfaces:** `load_manifest(path: Path) -> dict`；`generate_corpus(root: Path) -> list[dict]`；`summarize_results(manifest: dict, results: list[dict]) -> dict`。命令 `python3 scripts/acceptance_corpus.py validate examples/acceptance/manifest.json` 和 `generate <output-dir>`。

- [ ] 写失败测试：`test_generation_is_deterministic`、`test_record_variants_keep_every_id_once`（10/30/100 个 R0001 起有序 ID）、`test_target_errors_are_not_successes`、`test_duplicate_id_and_escaping_paths_fail`、`test_target_without_defined_api_is_blocked_design`，以及逐入口 applicability/错误码与完整资源负例覆盖测试。
- [ ] 跑 RED：`python3 -m unittest discover -s scripts -p 'test_acceptance_corpus.py' -v`，失败来自新模块/行为缺失，不来自网络/字体问题。
- [ ] 实现 B00 ASCII、B01 中英简报/面板、B02-10/30/100 段落记录（不是伪表格）、B03 跨页面板。精确页数/坐标留任务 3 实测审阅。目标分开登记粗体、两级列表、JPEG、10/30/100 行表格/重复表头、页眉页脚/页码、标题随首段及完整报告。只用独立有效探针检查当前拒绝；未定资源 API 的需求保持 blocked-design，不用一个组合输入的首个错误推断所有目标。
- [ ] 负例至少包含：过宽、零宽含文字（FPDF1302）；MaxPages（FPDF1303）；MaxInputCharacters（FPDF1001）；MaxElements（FPDF1002）；MaxDepth（FPDF1003）；MaxCssCharacters/MaxCssDeclarations（FPDF1205，分别案例）；MaxDisplayCommands 和 MaxOutputBytes（FPDF1401，分别案例）；高/低孤立代理项（FPDF1304）。额度都用合法正整数显式下调，避免参数无效异常掩盖正确资源诊断。每个例验证失败前输出保护。只适用 API 的例不伪造生产 CLI 选项。
- [ ] 跑 GREEN、validate、重复 generate 校验字节稳定；不修改批准参考。Commit `test: add deterministic document acceptance corpus and honest target states`。

## Task 2：两个真实入口与输出保护

**Interfaces:** `run_case(case: dict, entrypoint: str, executable: Path, font_paths: list[Path], out_dir: Path, timeout_s: float) -> dict`；`capture_environment(executables: dict[str, Path], font_paths: list[Path]) -> dict`。
宿主 `render <html> <pdf> [--font <ttf>] [--subset-fonts]`；宿主独有 `--max-pages <n>`、`--limit <whitelisted-PdfOptions-name>=<positive-int>`、`--probe-existing-output`、`--invalid-scalar <integer>`、`--pre-cancelled`。显式 Utf8JsonWriter 输出结构化 JSON，不用反射序列化，不污染生产 CLI stdout。

- [ ] 测试：`test_cli_and_api_use_identical_inputs_and_font_order`、`test_timeout_has_no_success_record_and_child_is_reaped`（真实测试子进程）、`test_binary_stdout_is_not_decoded_as_text`、`test_output_guard_preserves_existing_bytes`、`test_high_and_low_surrogates_are_constructed_inside_host`；逐入口限制、全部资源错误及取消不写出结果的集成探针。
- [ ] RED 同任务 1 命令改为 `test_acceptance_run.py`。
- [ ] 只调用现有 PdfConverter/PdfOptions/PdfFont；显式加载字体，记录 SHA、架构、SDK/runtime、动态代码状态、工具版本。CLI/API 等价用双方相同默认 A4/边距/字体/子集配置。单例独立目录、argv 不经 shell、进程树有边界；超时若不能确认清理就 infrastructure-error，不能成功。CLI 错误用已有 `--overwrite` 临时文件策略，API 用已有输出探针；invalid UTF-16 在运行时从整数构造。缺字体明确失败，不隐式下载资源，不宣称工作目录沙箱。
- [ ] GREEN：unittest；`dotnet build tools/FactsPDF.Acceptance/FactsPDF.Acceptance.csproj -c Release`；实际 B00/B01 两入口字节/文字对照、binary stdout、原有文件保护、全部负例和取消探针。Commit `test: add CLI and public API document acceptance runners`。

## Task 3：独立检查与真实参考审批

**Interfaces:** `inspect_pdf(pdf: Path, case: dict, environment: dict, out_dir: Path) -> dict`；`validate_reference(review: dict, case: dict, environment: dict) -> None`；`compare_evidence(reference: dict, candidate: dict, case: dict) -> dict`。
命令 `python3 scripts/acceptance_inspect.py capture --manifest <path> --runs <dir> --output <dir>` 与 `verify --manifest <path> --runs <dir> --reviews <json> --output <dir>`。

- [ ] 测试：`test_missing_or_reordered_record_fails`（删字/重复/换序、固定中文句）、`test_outside_page_box_is_rejected`、`test_invalid_xref_or_tool_failure_is_not_pass`、`test_environment_or_font_drift_requires_new_review`、`test_capture_cannot_approve_itself`、`test_target_needs_its_own_checks_before_verified`。
- [ ] RED：unittest 对应 `test_acceptance_inspect.py`。
- [ ] qpdf 检查结构；Poppler raw/bbox 文本与 PDF info 检查内容/页/边界；逐页 PNG 检查显示。文本真值来自独立合成源，不从待测 PDF 回抄；规范化只允许明确布局空白，不删除所有空格或标点掩错。复用 Chrome 独立参考方法，分别记录原输入/打印归一化哈希，浏览器不代替 FactsPDF 产物。字体与工具匹配记录；人工页面审批绑定 source/input/font/environment/PDF/text/PNG 指纹及真实审阅者。禁止一键 approve-current-output。
- [ ] GREEN 后实际 capture，检查全部页面，再取得用户页面审阅。未取得时保持 pending-review，任务冻结部分未完成；不造审批人。批准后保存允许分发的参考和审批记录，再用同构建 verify；人为损坏内容/指纹应失败。临时 artifact 不能是唯一持久来源。
- [ ] Commit `test: add independently reviewed report baselines and fail-closed comparison`；实际批准前可提交采集/检查基础代码，但不得标该任务已全部完成。

## Task 4：明确范围的性能采样与预算提案

**Interfaces:** `measure_cold(executable: Path, case: dict, fonts: list[Path], repeats: int, out_dir: Path) -> list[dict]`；`validate_measurements(data: dict) -> dict`；`summarize_measurements(data: dict) -> dict`。
宿主 `measure <html> --samples 30 --warmups 3 [--font <ttf>] [--subset-fonts]`。调度 `python3 scripts/acceptance_measure.py collect --manifest <path> --native-cli <path> --native-host <path> --font <ttf> --cold-samples 10 --warm-samples 30 --batches 2 --output <dir>`。

**每次热转换原始字段必填：** `sample_index`、`elapsed_ms`、`managed_allocated_bytes`、`pdf_bytes`、`pdf_sha256`、`pages`、`measurement_scope`。在同步同线程的 Convert 调用及输出 MemoryStream 建立之前读 GC.GetAllocatedBytesForCurrentThread，Convert 完成后立即读差值；不包含预读 HTML/字体和之后 JSON/hash/验证工作。资源加载另记 `font_load_elapsed_ms` 与 `font_load_managed_allocated_bytes`，明示是否含文件读取。它不是所有线程分配或峰值内存。

- [ ] 测试：`test_sample_counts_and_medians_are_recomputed`（偶数中位数）、`test_cold_runs_are_distinct_processes`、`test_unknown_peak_is_null_not_zero`、`test_hash_mismatch_fails_measurement`、`test_m8_legacy_and_m12_acceptance_baselines_cannot_be_relabelled`、`test_invalid_or_missing_samples_are_not_imputed`、`test_parallel_outputs_are_isolated`，以及 `test_missing_negative_or_noninteger_allocation_fails`、`test_allocation_summary_is_recomputed_from_raw_samples`。
- [ ] RED：unittest 对应 `test_acceptance_measure.py`。
- [ ] 实现并对独立检查通过的 B00/B01/B02-100 候选实际采两批，每批 10 个独立进程 cold、同进程 3 次未计 warm-up + 30 warm。候选未人工批准时测量明确标 provisional，不能称冻结基准。cold 从创建生产 CLI 到退出，包含启动/文件/字体读取/写 PDF，OS 缓存未主动清理；warm 复用资源且含转换/输出缓冲。Linux 外部采每进程峰值 RSS，宿主生命周期峰值另记；拿不到用 null 并说明，绝不补 0。内部阶段未公开就 not-measured，不改核心做计时。原始字段/source/环境/输入/字体/构建一致性全部验证，每次输出哈希/字节/页一致才进入统计。用两并发任务验证输出隔离，不声称极限吞吐。
- [ ] GREEN 后实际 collect，重新计算样本数、延迟与分配中位数；保留完整原始批次。记录真实 CPU/内存/OS，shared runner 只 provisional。候选预算逐项有测量依据和未批准状态，未有固定机器或用户预算审批不能声称性能验收完成。重复批次确认异常，不拿单次噪声造提升。
- [ ] Commit `test: measure document startup conversion and resources with explicit scopes`；不触动旧 M8 harness/基线。

## Task 5：CI 与可直接审阅的交付包

**Interface:** `python3 scripts/acceptance_run.py verify --manifest examples/acceptance/manifest.json --native-cli <path> --api-host <path> --font <ttf> --reviews docs/acceptance/reference-reviews.json --output artifacts/document-acceptance`。
`--strict-targets` 额外要求目标全验收；普通回归可在目标尚未实现时绿，但必须列出全部未实现目标，且 preview_ready=false。

- [ ] 测试：`test_summary_separates_regressions_errors_and_targets`、`test_strict_targets_fails_when_any_target_is_unverified`、`test_missing_review_fails_baseline_verify`、`test_artifact_allowlist_excludes_fonts_binaries_and_secrets`、`test_report_contains_exact_source_and_next_gap`，以及丢失适用入口/重复结果不被当成功。
- [ ] RED：`python3 -m unittest discover -s scripts -p 'test_acceptance_*.py' -v`。
- [ ] 新工作流 on pull_request/push(main)/workflow_dispatch，精确 checkout `${{ github.event.pull_request.head.sha || github.sha }}`，actions 使用仓库固定 SHA，contents:read、有 job/子进程超时。编译实际 Native CLI 和公共 API 宿主，记录明确字体/工具，不依赖隐藏资源。首轮 bootstrap 仅采候选并明确待审，不把它命名验收完成。登记用户审批后才改为默认 verify；不能自动回退 capture/跳过审批。上传白名单：合成模板/数据、PDF、PNG、提取文字、JSON/Markdown；不上传字体/二进制/整个目录。文档包含从零 CLI/API 复现、binary stdout、错误、资源和指标范围，每个支持项链接实际 case。
- [ ] 新单测、`dotnet test FactsPDF.slnx -c Release`、现有 CSS/font/subset/Chrome/资源/CodeQL，以及新 workflow 全部在最终 HEAD 实跑。审阅所有页和原始测量，仍缺用户审批/固定预算则报告真实未满足项，不宣称 A 或预览已完成。
- [ ] 最终独立代码审查，问题先复现再修。确认 diff 无 src/、旧基准改名、许可/发布动作。在门槛满足后按既定授权合并，核对实际 main 后才允许 B。若用户页面审批尚未得到，保留实现 PR 和证据包供审阅，不伪造合并许可或证据。

## 完成与交接

A 交付可复现语料、独立检查后的页面包、未支持目标清单、CLI/API/错误对照、真实采样与预算提案；不是整个 Developer Preview 1 完成。
本计划已获执行批准。四项复审修订：批准状态与 scope 同步；逐样本托管分配和汇总校验；补全资源/零宽负例；每个案例逐入口可执行合同。方法/范围不变。
自审映射：任务1场景与诚实分类；任务2入口/输出保护；任务3真值/审阅；任务4原始分配/延迟/RSS/预算；任务5报告/CI/顺序合并。页面审批和数值预算仍须基于真实产物，不能预填。