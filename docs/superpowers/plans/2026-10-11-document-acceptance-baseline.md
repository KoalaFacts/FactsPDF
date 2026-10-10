# FactsPDF 工作包 A：真实文档验收基线 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. 若执行环境确有可用子代理且用户选择该方式，可改用 superpowers:subagent-driven-development。Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 不改引擎，交付可复现的报告验收集、真实支持矩阵与分清测量范围的性能基线，让后续每项能力有明确验收对象。

**Architecture:** 一套确定性合成语料与清单驱动现有 CLI、一个仅调用公共 API 的验收宿主及独立 PDF 检查。已有能力、期望错误和未实现目标分别统计；参考输出先采集、独立核验及页面审阅，再登记为冻结基线，禁止自动把当前输出当真值。

**Tech Stack:** 仓库现有 .NET 10/C#、Python 标准库 unittest、Linux x64 Native AOT；沿用已有验证环境的 qpdf、Poppler、Chrome、Pillow。它们只用于检查，不成为 FactsPDF 运行时依赖。

**Spec:** `docs/superpowers/specs/2026-10-11-first-usable-release-scope-design.md`，主要实现第 5/6 节和第 7 节工作包 A；B–G 仅保留需求引用，不在本计划实施。

**状态与基线:** 用户在范围稿展示后回复“好”，确认该范围，允许展开 A 的实施计划。本计划是新交付，待审阅；未开始执行下列任务。引擎固定对照 M12 `1e3f24f21b2e9bbdc5db6e05066998c07a6f9af6`，范围文件来自 `b80d7891368de03c7a9c715650fd070cc7480fc9`。不预占新的 Mxx 编号。

## Global Constraints

- “第一项实施工作是建立文档基线，不先改引擎。”
- “基线/目标案例分离，独立检查方法、初始测量与预算提案可审阅”。
- “不得将其 expected-error 或跳过状态计为功能通过。”
- “样本的精确页数与几何期待值必须在作者编写样本后，经独立检查和人工审阅冻结”。
- “不修改引擎、测试、工作流、依赖、公共 API 或许可”是范围稿提交当轮的限制；A 执行时只新增验收语料、验证宿主/测试、文档与独立验收工作流，不修改 `src/` 引擎、既有测试期待值、现有基准或许可。
- “字体作为显式测试资源记录授权来源和指纹，不向用户分发字体文件。”
- “前一实现未合并和验证，不创建下一实现 PR。”
- “本稿不虚构绝对毫秒、RSS 或 QPS 保证。”数字预算须以实际原始样本为依据，未审阅前不得作为已达成的性能承诺。
- 现有 `.github/workflows/box-layout-resources.yml` 的 M8 固定基线和 `benchmarks/FactsPDF.Benchmarks/Program.cs` 均保持原样；A 新报告明确标记自己的 M12 基线，不能重命名旧基准。
- A 只提供 Linux x64 实际 Native AOT 验收；继续运行原有跨平台测试，但不在本工作包交付 Windows AOT、Node SDK、浏览器 WASM 或公开包。

## Review Focus

1. 未实现目标被跳过、返回预期错误或只生成部分内容：不能被记为已支持；任务 1/3 用分类与缺失能力测试锁定。
2. 字体或工具版本变化：同样 HTML 不能沿用旧哈希承诺；任务 2/3 检查资源指纹和环境标识，不自动刷新参考。
3. 中文缺失、文本重复、顺序错误与页面外内容：PDF 可打开不代表正确；任务 3 用独立提取、合成记录序列及破坏性负样本检查。
4. 转换失败或进程超时：不能覆盖调用方已有文件、遗留成功状态或混淆 stderr/PDF 输出；任务 2 检验文件和字节流行为。
5. 测量遗漏加载成本、造假样本数量或无法取得峰值：必须显示真实范围及缺失值；任务 4 保留原始样本并重新计算汇总，绝不以 0 冒充未测量。

## 文件与职责

下列为**待创建**路径，不是现有实现。每个模块只承担一项职责；如单模块明显膨胀，优先按下述职责拆分，不重构引擎。

| 路径 | 职责 |
| --- | --- |
| `examples/acceptance/manifest.json` | 样本 ID、类型、需求、输入、资源角色与校验要求；不保存伪造页数 |
| `examples/acceptance/data/report-records.json` | 合成中英记录及稳定 ID，无客户数据 |
| `examples/acceptance/baseline/` | 冻结的已支持 HTML：ASCII 控制、双语简报、正文增长变体、跨页信息面板 |
| `examples/acceptance/targets/requirements.json` | 首版未实现能力与完整报告的验收需求，不提前发明资源 API |
| `examples/acceptance/negative/` | 独立超宽段落、页数超限、非法 UTF-16 宿主输入的案例声明 |
| `scripts/acceptance_corpus.py` | 清单校验、确定性生成、输入哈希与目标状态汇总 |
| `scripts/acceptance_run.py` | 隔离运行 CLI/API 宿主，输出原始运行记录 |
| `scripts/acceptance_inspect.py` | PDF 结构/文字/页面检查、参考来源验证与冻结记录验证 |
| `scripts/acceptance_measure.py` | 冷/热测量调度、原始样本校验与统计，不包含引擎代码 |
| `scripts/test_acceptance_*.py` | 与上述四个模块对应的 unittest，不用网络或真实字体做单元测试 |
| `tools/FactsPDF.Acceptance/FactsPDF.Acceptance.csproj`、`Program.cs` | 独立 .NET 10 公共 API 验收宿主，ProjectReference 指向 `../../src/FactsPDF/FactsPDF.csproj`，无 PackageReference |
| `tools/FactsPDF.Acceptance/RenderCommand.cs`、`MeasureCommand.cs` | 单次转换/失败前输出探针与同进程热样本；显式 Utf8JsonWriter，不使用反射序列化 |
| `docs/acceptance/README.md`、`support-matrix.md`、`performance-baseline.md` | 从零复现步骤、支持矩阵、测量说明与预算提案 |
| `docs/acceptance/reference-reviews.json` | 经独立检查和真实审阅登记的参考清单；未审阅时明确为空 |
| `.github/workflows/document-acceptance.yml` | 独立验收流程，提交 SHA 明确，上传允许公开的证据，不影响旧工作流基线 |

完整字体文件、原生二进制、私有路径和凭据不进入上传清单。PDF/页面图在 `artifacts/document-acceptance/` 中生成；冻结记录保存哈希、审阅来源和持久可取的参考位置，不把短期 Actions 链接当永久唯一备份。

## 共享数据约定

`schema_version=1`。Case 的 `kind` 只能是 `baseline`、`negative`、`target`；三者分母不能相加当“功能通过率”。每个 Case 有唯一 `id`、`requirements`、`input_path`（待设计目标可为 null）、`records`、`font_roles`、`checks`。文件引用必须是验收目录内的相对路径，禁止绝对路径和 `..` 逃逸。

- 基线结果：`passed` / `regression` / `pending-review` / `infrastructure-error`。
- 错误案例结果：`expected-error` / `unexpected-success` / `wrong-error` / `infrastructure-error`；单独统计。
- 目标结果：`unsupported` / `blocked-design` / `candidate` / `verified`。只有真实成功、全部专属断言满足、独立检查与审阅关联齐全才可成为 `verified`。新成功不会自动视为完成。
- 运行记录含 `case_id`、`entrypoint`、`source_sha`、`build_mode`、`input_sha256`、有序字体指纹、`environment_id`、退出码/结构化错误、输出字节与哈希、耗时、超时/取消结果；记录不可被汇总报告静默覆盖。
- 汇总分别给出 `baseline_regression_passed`、`target_verified_count`、`target_total`、`evidence_complete`。A 不发放 `preview_ready=true`；预览版发布条件由工作包 G 独立判定。
- `capture` 只能采集候选参考，不能修改 `reference-reviews.json`；`verify` 遇到缺少有效冻结记录必须失败，不能降级为截图演示。

---

### Task 1: 建立可重复语料和诚实状态清单

**Files:** 创建 `examples/acceptance/` 上述清单/数据/HTML/需求文件、`scripts/acceptance_corpus.py`、`scripts/test_acceptance_corpus.py`。

**Interfaces:** `load_manifest(path: Path) -> dict`；`generate_corpus(root: Path) -> list[dict]`；`summarize_results(manifest: dict, results: list[dict]) -> dict`。CLI：`python3 scripts/acceptance_corpus.py validate examples/acceptance/manifest.json`；`generate <output-dir>`。生成为纯标准库操作，显式输出，禁止替换已批准参考。

- [ ] **Step 1: 写失败测试。** `test_generation_is_deterministic` 比较两次生成文件字节和输入哈希；`test_record_variants_keep_every_id_once` 断言 10/30/100 变体分别完整保留 `R0001` 起的有序 ID；`test_target_errors_are_not_successes` 断言未支持目标不增加 verified；`test_duplicate_id_and_escaping_paths_fail` 断言非法清单被拒绝；`test_target_without_defined_api_is_blocked_design` 不得生成虚构的成功命令。
- [ ] **Step 2: 执行 RED。** `python3 -m unittest discover -s scripts -p 'test_acceptance_corpus.py' -v`。应在新模块/接口未实现时失败，不能因为字体或网络失败伪称 RED。
- [ ] **Step 3: 实现清单与语料。** B00 ASCII 控制；B01 双语简报（标题、摘要、正文、嵌套信息面板）；B02-10/30/100 用现有段落表示记录、不是伪造表格；B03 显式跨页信息面板。精确参考页数在任务 3 冻结，不在这里猜。目标需求分别登记真实粗体、两级列表、JPEG、10/30/100 行表格与重复表头、页眉页脚/页码、标题随首段及完整组合报告；未定公共语法以需求记录表示。可用的隔离 `<ul>`/`<table>` 等探针仅证明当前不支持，不能用一个组合报告的首个错误推导所有能力状态。单据、照片、真实业务计算均不加入。
- [ ] **Step 4: GREEN。** 运行上述 unittest 和 `validate`，再重新生成一次；受版本控制的语料应无差异。目标总数包括 blocked-design，不能省略它们缩小分母。
- [ ] **Step 5: Commit。** `test: add deterministic document acceptance corpus and honest target states`。交付可验证输入及缺口清单，不宣称渲染已通过。

### Task 2: 从 CLI 和公共 .NET API 运行同一份输入

**Files:** 创建 `scripts/acceptance_run.py`、`scripts/test_acceptance_run.py` 和 `tools/FactsPDF.Acceptance/`；不改 `src/` 或现有 benchmark。

**Interfaces:** `run_case(case: dict, entrypoint: str, executable: Path, font_paths: list[Path], out_dir: Path, timeout_s: float) -> dict`；`capture_environment(executables: dict[str, Path], font_paths: list[Path]) -> dict`。验收宿主：`render <html> <pdf> [--font <ttf>] [--subset-fonts]`；诊断测试参数仅属宿主，支持 `--max-pages <n>`、`--probe-existing-output`、`--invalid-scalar <integer>`；成功或失败都输出结构化 JSON，不污染生产 CLI 的二进制 stdout。

- [ ] **Step 1: 写失败测试。** `test_cli_and_api_use_identical_inputs_and_font_order` 校验参数顺序与哈希；`test_timeout_has_no_success_record_and_child_is_reaped` 用测试子进程验证超时退出/清理；`test_binary_stdout_is_not_decoded_as_text` 校验二进制保存与 stderr 分离；`test_output_guard_preserves_existing_bytes` 针对过宽段落和 `--max-pages 1` 验证 `keep` 内容保持；`test_high_and_low_surrogates_are_constructed_inside_host` 运行时用整数构造非法字符，不把它放进 UTF-8 JSON/属性元数据。
- [ ] **Step 2: RED。** `python3 -m unittest discover -s scripts -p 'test_acceptance_run.py' -v`。
- [ ] **Step 3: 实现适配器和宿主。** 仅通过 `PdfConverter.Convert`、`PdfOptions`、`PdfFont.LoadTrueType` 调用已有核心，显式加载字体；记录完整 SHA、实际进程架构、SDK/runtime、动态代码状态和验证工具版本。CLI 比较只用双方都能表达的相同默认 A4/边距配置。各例使用独立目录，argv 不经 shell 拼接；超时如不能清理完整进程树则标 infrastructure-error，不继续伪造成功。不自动下载字体，缺失资源明确报错。固定受信任工作目录，不宣称主机沙箱。
- [ ] **Step 4: GREEN 与真实冒烟。** 跑 unittest；`dotnet build tools/FactsPDF.Acceptance/FactsPDF.Acceptance.csproj -c Release`；对 B00/B01 通过生产 CLI 和验收宿主各渲染一次，同环境比较字节、提取文字。用 `--probe-existing-output` 实测错误字节保护；CLI 用其原有临时文件策略验证已有目标文件不受损。
- [ ] **Step 5: Commit。** `test: add CLI and public API document acceptance runners`。宿主无新增 NuGet 包、无内部反射入口、无第二套排版。

### Task 3: 独立验证页面，冻结参考而不是抄录输出

**Files:** 创建 `scripts/acceptance_inspect.py`、`scripts/test_acceptance_inspect.py`、`docs/acceptance/reference-reviews.json` 与 `docs/acceptance/README.md` 初版。

**Interfaces:** `inspect_pdf(pdf: Path, case: dict, environment: dict, out_dir: Path) -> dict`；`validate_reference(review: dict, case: dict, environment: dict) -> None`；`compare_evidence(reference: dict, candidate: dict, case: dict) -> dict`。命令：`python3 scripts/acceptance_inspect.py capture --manifest <path> --runs <dir> --output <dir>`；`verify --manifest <path> --runs <dir> --reviews <json> --output <dir>`。

- [ ] **Step 1: 写失败测试。** `test_missing_or_reordered_record_fails` 用删字/重复/换序提取结果检验记录 ID 与中文固定句；`test_outside_page_box_is_rejected` 检查文本 bbox 和可绘制区域；`test_invalid_xref_or_tool_failure_is_not_pass`；`test_environment_or_font_drift_requires_new_review`；`test_capture_cannot_approve_itself`；`test_target_needs_its_own_checks_before_verified`。PNG 差异、文字断言、结构检查不能互相替代。
- [ ] **Step 2: RED。** `python3 -m unittest discover -s scripts -p 'test_acceptance_inspect.py' -v`。
- [ ] **Step 3: 实现独立检查。** 用 qpdf 检查结构，Poppler `pdftotext -bbox-layout`/纯文本检查内容与边界，`pdfinfo` 记录页面，`pdftoppm` 生成逐页图；字体和工具必须来自记录的环境。精确文本期待由合成源数据独立推导，不从待测 PDF 复制；仅允许显式布局空白规范化，不删除所有空格或丢弃标点掩盖问题。复用原有 Chrome 对照方法制作独立参考，原输入与打印归一化分别保存哈希；浏览器只作为参照，不能替引擎渲染。基线页数、关键坐标和人工页面审阅结论由独立检查后登记，禁止 `--approve-current-output` 一键生成。审阅记录绑定 source/input/font/environment/PDF/text/PNG 哈希和可追溯审阅者，不冒充用户已审阅。
- [ ] **Step 4: GREEN 与冻结。** 跑 unittest；先 `capture`，查看全部页面并由独立审阅核验无缺字、重叠、截断和幽灵页，再提交真实审批记录和允许分发的参考产物。若用户页面审阅尚未取得，保持 pending-review；此时任务未完成，不能填造审批人。用同一构建再次 `verify`，固定环境未变时字节相同；人为破坏文本或指纹必须失败。只保存一种批准参考来源，不将过期临时 artifact 链接作为唯一参考。
- [ ] **Step 5: Commit。** `test: add independently reviewed report baselines and fail-closed comparison`。提交必须注明实际参考来源，不含字体文件。

### Task 4: 测完整负载并提出预算，不修改旧基准

**Files:** 创建 `scripts/acceptance_measure.py`、`scripts/test_acceptance_measure.py`、宿主 `MeasureCommand.cs`、`docs/acceptance/performance-baseline.md`。

**Interfaces:** `measure_cold(executable: Path, case: dict, fonts: list[Path], repeats: int, out_dir: Path) -> list[dict]`；`validate_measurements(data: dict) -> dict`；`summarize_measurements(data: dict) -> dict`。宿主 `measure <html> --samples 30 --warmups 3 [--font <ttf>] [--subset-fonts]` 输出原始逐次 JSON，含显式 measurement_scope。外部调度命令 `python3 scripts/acceptance_measure.py collect --manifest <path> --native-cli <path> --native-host <path> --font <ttf> --cold-samples 10 --warm-samples 30 --batches 2 --output <dir>`。

- [ ] **Step 1: 写失败测试。** `test_sample_counts_and_medians_are_recomputed` 用偶数样本验证中位数；`test_cold_runs_are_distinct_processes`；`test_unknown_peak_is_null_not_zero`；`test_hash_mismatch_fails_measurement`；`test_m8_legacy_and_m12_acceptance_baselines_cannot_be_relabelled`；`test_invalid_or_missing_samples_are_not_imputed`；`test_parallel_outputs_are_isolated`。
- [ ] **Step 2: RED。** `python3 -m unittest discover -s scripts -p 'test_acceptance_measure.py' -v`。
- [ ] **Step 3: 实现采样。** 对已通过基线的 B00、B01、B02-100 至少采集两批：每批 10 个独立进程冷启动、同进程 3 次未计入 warm-up 后 30 次热转换。冷启动计时从启动生产 Native CLI 前至其退出，包含进程/文件/字体读取和写 PDF；标明未主动清 OS 文件缓存。热转换复用已加载资源，包含转换及输出缓冲；字体加载另测且注明是否包含文件读取，不标作端到端冷启动。Linux 外部按单进程采集峰值 RSS，热宿主另记进程生命周期峰值，均记录范围而非假称纯排版峰值。布局内部分段若公共 API 不暴露，记 not-measured，不为了计时修改引擎。每批记录原始样本、输入/字体/工具/机器指纹和构建模式。无效结果一律不进入中位数。额外用固定两个并发任务验证输出隔离，不据此宣称极限吞吐。
- [ ] **Step 4: GREEN 与测量。** 跑 unittest；真实 `collect` 后重算中位数与样本数，核对每次 PDF 的字节数/页数/哈希。指定参考环境记录机器型号/CPU/内存/OS/工具链；共享 GitHub runner 只产生 provisional 观察，不谎称固定硬件。预算稿逐项给原始依据、候选阈值、测量范围和未测项目；没有合适参考机器或审批时预算保持未批准，预览性能验收不通过。延迟告警需重复批次确认，不能以一次共享 CI 波动阻断全部开发或夸大提升。
- [ ] **Step 5: Commit。** `test: measure document startup conversion and resources with explicit scopes`。保持旧 M8 harness/workflow/样本不变，新增对照单独命名。

### Task 5: 接入 CI，交付一份能直接审阅的报告

**Files:** 创建 `.github/workflows/document-acceptance.yml`；完成 `docs/acceptance/README.md`、`support-matrix.md`、`performance-baseline.md`；补 `scripts/test_acceptance_corpus.py` 的汇总/上传测试。

**Interfaces:** 统一 orchestration CLI：`python3 scripts/acceptance_run.py verify --manifest examples/acceptance/manifest.json --native-cli <path> --api-host <path> --font <ttf> --reviews docs/acceptance/reference-reviews.json --output artifacts/document-acceptance`。`--strict-targets` 供预览版门槛使用；A 的普通回归 job 可在目标未支持时绿，但必须输出目标未完成数量和预览未验收，严禁把此状态等同发布就绪。

- [ ] **Step 1: 写失败测试。** `test_summary_separates_regressions_errors_and_targets`；`test_strict_targets_fails_when_any_target_is_unverified`；`test_missing_review_fails_baseline_verify`；`test_artifact_allowlist_excludes_fonts_binaries_and_secrets`；`test_report_contains_exact_source_and_next_gap`。审批资料不存在时不得把 `verify` 自动转成 `capture`。
- [ ] **Step 2: RED。** `python3 -m unittest discover -s scripts -p 'test_acceptance_*.py' -v`。
- [ ] **Step 3: 实现 CI 与使用文档。** 工作流触发 `pull_request`、`push(main)`、`workflow_dispatch`；checkout 精确 `${{ github.event.pull_request.head.sha || github.sha }}`，actions 固定到仓库已有 SHA，权限 `contents: read`，有明确 job/子进程超时。Linux 发布真正的 CLI 和验收宿主 Native AOT，复用已有受控验证依赖并记录实际字体指纹，不悄悄依赖本地未声明字体。首轮 bootstrap 只采候选产物并明确未完成；登记审批后固定默认 verify，不能靠默认跳过让门槛虚绿。上传采用文件白名单：模板/合成数据、PDF、PNG、提取文字、JSON/Markdown 证据；不上传字体、二进制或整个工作目录。支持矩阵每项链接实际 case 与证据，不写百分比“CSS 兼容度”。写清公开 API 示例、CLI binary stdout、错误、字体提供方式及冷/热指标含义。
- [ ] **Step 4: 全部验证。** 新 unittest 全部通过；`dotnet test FactsPDF.slnx -c Release`；新验收 job、原有 CSS/字体/子集/Chrome/资源证据及 CodeQL 在最终 HEAD 上实际通过。确认 manifest 的目标仍未被计成完成。审阅每份 baseline PDF 的所有页及测量原始数据；无审批/预算证据则列出阻塞而非声称退出条件满足。
- [ ] **Step 5: Commit 与独立审查。** `test: gate report baselines and publish honest acceptance evidence`。独立审查最终代码 SHA；对 review findings 先复现再修正。确认 diff 不含 `src/`、旧基准改名、许可或发布动作；按既定授权合并工作包 A，实现合并后的 main 验证与确切状态汇报，之后才允许工作包 B 的独立设计/验证开始。

## 交付验收与执行交接

A 的交付包包括：可以从零重跑的合成文档、经过审阅的已支持案例输出、仍未支持的目标清单、CLI/API 对照、真实错误路径、带原始数据的测量报告与候选预算。**这不表示 Developer Preview 1 已完成**，也不是 JPEG、表格、WASM 或 Windows AOT 已实现。

本轮只把本计划加入同一规划 PR #19，保持 Draft、main 不变，不创建实现 PR，不运行样本或报告尚未发生的测试。计划审阅后，先完成规划 PR 的审查/合并与 main 核对，再从实际基线创建工作包 A 的唯一实现 PR。沿用当前执行方式：当前会话逐项实现，整支最终 HEAD 独立审查，修复后合并和 main 验证；不要求重新授权每次已约定的合并。

**自审映射：** 场景/清单与目标分离→任务 1；入口与输出保护→任务 2；独立真值/字体指纹/逐页审阅→任务 3；冷/热/RSS/隔离与预算→任务 4；支持矩阵/从零复现/CI/顺序合并→任务 5。范围 B–G 均不在本实现计划；尚未实测的页数、性能值、资源指纹及人工审批均未编造。
