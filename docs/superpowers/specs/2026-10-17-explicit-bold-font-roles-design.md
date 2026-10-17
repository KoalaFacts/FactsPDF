# C1：显式字体样式选择与验收契约

日期：2026-10-17。代码基线：`f31e4806be4951134921ab9722dc2fee639e585e`（PR27 已合并）。

状态：用户已在粗体草案 `7d3eb61` 后明确同意按最新讨论推进：保留现有按字符缺字 fallback，补 normal、bold、italic、bold-italic 四种显式角色；先修订设计并独立审查，再按可验收增量实现。本稿取代原稿的“只设计双链、斜体排除、等待 D1”状态。公共名称在实现完成前仍是本阶段约定，不是当前 main 支持声明。

## 1. 目的与边界

在现有转换器、字体加载器、fallback 和布局上补样式选择，使中英业务报告可以显式指定强调所用字体。角色内按标量和输入顺序缺字回退，不要求字体成套、同一家族、同名或链等长。静态 TrueType 字体本身可以是普通、粗体、斜体或粗斜体；“静态”不等于“普通”。

[范围设计](2026-10-11-first-usable-release-scope-design.md) §3 的显式资源、旧调用兼容、不伪造，以及 §7 的粗体/列表独立验收、§8 的公共接口审阅仍适用；本次用户讨论明确把斜体/粗斜体纳入 C1，覆盖此前“斜体另排”的边界。列表、图片、表格、字体家族匹配、全字重轴、variable fonts、复杂塑形和发布不纳入。

默认输入确定性：仅使用明确给定字节/路径。受支持、满足嵌入限制的系统字体文件可以通过既有加载器/CLI 显式指定；不枚举系统字体、不按名称猜测、不联网发现。不合成加粗、倾斜或粗斜体，不静默降级普通体。现有 API 没有 warning 集合，本次不虚构降级警告或引入降级策略。

## 2. 小范围公共约定

保留 `PdfOptions.Fonts` / CLI `--font` 的完整原意与顺序；新增三条默认空的 `IReadOnlyList<PdfFont>` init 属性及重复参数：

| 计算角色 | .NET 链 | CLI 参数 |
| --- | --- | --- |
| normal（400、normal） | `Fonts` | `--font` |
| bold（700、normal） | `BoldFonts` | `--font-bold` |
| italic（400、italic） | `ItalicFonts` | `--font-italic` |
| bold-italic（700、italic） | `BoldItalicFonts` | `--font-bold-italic` |

角色属于本次转换配置，不修改 `PdfFont` 对象。新增样式不改 `PdfFont.LoadTrueType` 或 `PdfConverter.Convert` 签名。显式链可以只有某一角色；不能强制配齐四组，也不能强制存在常规资源。

```csharp
// 本阶段接口约定；资源由调用方通过现有加载器加载。
var options = new PdfOptions {
    Fonts = [latinRegular, cjkRegular],
    BoldFonts = [latinBold, cjkBold],
    ItalicFonts = [latinItalic, cjkItalic],
    BoldItalicFonts = [latinBoldItalic, cjkBoldItalic],
    SubsetFonts = true
};
```

`Fonts` 为空仍保留原有 ASCII/Courier 行为；普通非 ASCII 仍报 `FPDF1301`，不从其他角色借字。允许一个 PDF 混用该原有 Courier 普通文本与显式样式字体；这需要在已有 Unicode 序列化器中容纳一个已有的 Courier 资源，不另写字体系统。无嵌入字体时仍用原 ASCII 路径，常规旧输入的字节保持不变。

四链合计至多 8 个声明项（去重前计数），保留旧常规链 8 项容量。四链唯一字体对象合计受原 `MaxTotalFontBytes` 限制，默认 64 MiB；每次加载仍默认至多 32 MiB。同一对象跨角色只计一次字节；不同对象即使字节相同仍分别计入。null 集合/条目、超过项数为参数错误；字节额度为 `FPDF1503`。快照各链，禁止调用方并发修改集合；不可变字体仍可并发复用。

各角色链内按声明顺序回退，角色间绝不互相回退。样式映射不检查或推断文件名/家族/字重元数据：调用方负责角色声明正确。把普通字体显式放进 italic 链不产生自动倾斜，也不作为真实斜体验收证据。真实样张的字体来源与样式由独立工具和人工核对。

CLI 旗标可交错，各链保留自己的声明顺序；按总体参数顺序读取。所有角色字体路径都进入既有输出同路径保护，`--overwrite` 不解除保护。可信工作目录、别名、并发文件替换的既有边界不变。

## 3. HTML/CSS 选择

新增行内容器 `b` / `strong` 默认绝对 bold，`i` / `em` 默认 italic；两属性独立继承，因此嵌套 `<strong><em>…</em></strong>` 选择 bold-italic。行内容器沿用 span 的布局/合法嵌套约束，不插空格、不新增换行点、不容许块级子元素；重复嵌套不继续加重或加斜。标签默认值低于作者 CSS。

- `font-weight`：normal / bold / 数值 400 / 700 / inherit / initial / unset；400.0 等数值相同的无单位 number token 等价。其他字重、bolder/lighter、百分比、函数明确 `FPDF1202`。
- `font-style`：normal / italic / inherit / initial / unset。oblique（含角度）、其它关键字/函数明确 `FPDF1202`；不通过剪切矩阵模拟斜体。
- 两属性都是继承属性，initial 是 normal；unset 等于继承。使用既有内联/内嵌样式、选择器和 `!important` 层叠。br/metadata 等适用边界不变；未知/非法 CSS 即使不命中也严格拒绝。
- h1–h6 保留原字号与字重默认行为。font 简写、font-family、@font-face、font-synthesis-*、font-variation-settings 不新增支持。

有限子集与浏览器默认值不同：CSS normal/bold 对应 400/700；HTML 默认 b/strong 是相对加重，本引擎只做绝对两档。不得宣称完整浏览器字体匹配或所有 CSS 值兼容。[W3C 字重定义](https://www.w3.org/TR/2026/WD-css-fonts-4-20261016/#font-weight-prop)、[WHATWG 默认呈现](https://html.spec.whatwg.org/multipage/rendering.html#phrasing-content-3)。

## 4. 核心选择、布局与输出

新增内部四值角色，由计算 weight/style 决定；缓存键包含角色与标量，在正确角色链内找第一个有字形的字体。普通 Font 列表里的静态 italic 字体继续原样使用，不自动改角色或拒绝旧调用。

测量必须用最终字体实际 advance；既有角色内缺字、CJK 标点、NBSP、单词不断开规则保持。`<span>oper</span><strong>ation</strong>` 仍是连续单词，不能按样式边界断开。行盒延续既有首字体度量/line-height/leading/段落 strut 模型，但使用每个样式的角色链；normal 链为空时保留原 Courier 度量。不能改成所有 fallback 字体的最大指标而破坏旧基线。

非 normal 角色参与文字解析或行 strut 时必须有该链；空且不产生行盒的元素不误报。既有待定空格在原位置解析的行为保持。错误定位锚定需要角色的元素起始标签，根/无源位置保持 -1，不伪造逐字符精确位置。

PDF 字体按真实对象身份使用，不能凭 PostScript 名称合并不同字体。全嵌入/子集、ToUnicode、真实宽度、嵌入许可及额度复用现有逻辑。不同角色指向同一个显式对象可以共享资源。不能使用描边、错位重绘、位图或剪切来合成样式。

兼容门槛：只使用既有输入/常规 Fonts 的调用，成功 PDF 字节与旧基线一致，原错误及默认值不变；添加合法未使用样式资源也不改变 PDF 字节。新 b/strong/i/em 或 CSS 输入域可以从旧“不支持”转为成功或缺资源错误；设计或编译通过本身不改变当前支持矩阵。

## 5. 诊断与失败不留半成品

新增 `FPDF1507`：需要 bold、italic 或 bold-italic，但该角色链为空；消息必须包含准确角色，不能借用其它样式。角色链存在但缺字仍为 `FPDF1504`：新增非 normal 角色的消息带角色与 U+ 标量，并与 `FPDF1507` 一样提供可用元素源位置；normal 保持旧消息 `No supplied font contains U+....` 和 SourceOffset=-1。原 normal/Courier 的非 ASCII 错误 `FPDF1301` 保持。

字体结构/格式/总量/许可/子集错误保持 `FPDF1501/1502/1503/1505/1506`。variable/CFF/集合等原不支持表示仍拒绝；不把显式系统路径当作绕过格式或许可检查的入口。非法列表参数是 ArgumentException / CLI 退出 2；字体文件 I/O 是 CLI 退出 4；核心错误退出 3，取消 130，成功 0。

私有 WASM 宿主传入四条明确的字体字节链，仍调用同一核心，并保留旧双字体常规入口。它不是公共 JS SDK。HTTP/空资源等宿主诊断与核心缺角色分开；原 core code/message 原样返回，失败 pdfBase64 为 null。配置异常以私有 `B2_INPUT_FONTS` 诊断，不增加公共 warning 集合。

字体/语法/布局/资源上限/序列化等最终复制前失败：.NET 输出内容和位置不变；CLI stdout 无 PDF，已有文件即使 overwrite 也保留，新目标不存在，临时文件清理；WASM 不返回/下载部分 PDF，不泄漏上次成功结果。最终复制期间任意流 I/O 错误或取消可能已写前缀，不能回滚的原限制明确保留；CLI 文件仍通过完整转换后的同目录临时文件替换保护目标。清理或替换失败如实报告。

## 6. 首个可验收增量

一个 C1 实现 PR 完成四角色核心、CLI 参数、私有 WASM 适配与实际证据；不拆出“只声明 API 没有用法”的实现 PR，也不同时开启列表阶段。引擎复用既有 fallback 和字体序列化，仅补角色选择及必要的混合 Courier 输出。

首个小结果先给出：设计独立审查结论，随后给出通过的核心/CLI 红绿测试和第一份真实样张；再完成同最终 SHA 的全部门槛。用户已授权顺序执行与正常 PR 合并，不追加设计/计划许可问题。若发现与该方向冲突的实质接口问题，先给具体方案和证据。

## 7. 真实样张与案例契约

使用合成运营报告：常规“北京团队 / Beijing team”，粗体“复核通过 / Review approved”，斜体“备注 café / Note café”，粗斜体“优先处理 / Priority action”。角色字体可以来自不同家族，各自声明 Latin/CJK 顺序；真实字体必须是受支持静态 TrueType（glyf），保存版本、来源、许可依据、SHA-256、角色与顺序。不分发独立字体文件，不用普通 CJK 充当缺失样式；若某样式无合格 CJK 资源，记录该样张阻塞而非降级。

固定 A4、36pt 页边距、12pt、line-height 1.5、190pt 内容宽、8pt padding、1pt 实线面板。短报告包含四角色、嵌套覆盖、跨角色连续单词与显式 break-before:page。另有一个跨页面板，按 001–060 排列记录，每条包含四角色短语；第三例用单个 italic/bold-italic 长段落验证同一 run 自然跨行跨页。必须实际发生目标换行/分页；页数/坐标在固定字体生成后独立审阅记录，不从引擎输出自动冻结真值。

| ID | 覆盖 | 必须证明 |
| --- | --- | --- |
| C1-01 | 四角色中英、数字、é，真静态字体 | 实际字体对象/字形/advance 正确，无合成；文字可读 |
| C1-02 | b/strong/i/em、CSS 两属性、嵌套组合 | 对等输入的角色/几何一致；两属性独立，不将 bold-italic 降到单样式 |
| C1-03 | 同字四角色交替、inherit/initial/unset/important、重复嵌套 | 无缓存污染或额外加重；覆盖后正确恢复 |
| C1-04 | 60 记录、单样式长 run 自然分页、显式分页 | 记录不丢不重；跨页角色、行高、边框正确，无截断/重叠/幽灵页 |
| C1-05 | 跨角色英文词、NBSP、CJK 标点 | 不增加断词/空格，沿用原规则 |
| C1-06 | 旧普通调用、静态 italic 文件放在旧 Fonts、未使用新增资源 | 保留原字节/顺序/错误；“静态”不被错误解释为普通体 |
| C1-07 | 分别缺 bold/italic/bold-italic；仅提供一种角色；嵌套双样式仅有单样式资源 | 缺角色 FPDF1507 含角色；不要求成套；不做跨角色降级 |
| C1-08 | 某角色仅 Latin、其它角色有 CJK；normal 无字体的中文 | 分别 FPDF1504（准确角色/标量）或原 FPDF1301；无自动系统搜索 |
| C1-09 | 每角色链内首项缺字第二项有字、不同家族/同名不同字体 | 有序 fallback，按真实字体身份选择；无配对要求 |
| C1-10 | 600/bolder/oblique/字体家族/variable 输入等独立负例 | 正确类别拒绝，未命中非法 CSS 不忽略；variable 未支持 |
| C1-11 | 缺文件、HTTP、空/损坏/受限字体（含未使用但显式加载资源） | 资源和核心错误可区分、无半成品 |
| C1-12 | 总 8/9 项、总字节额度边界、输出同字体路径、真实样式宽度超宽/页数/输出预算 | 原额度不因角色翻倍；错误准确 |
| C1-13 | 每类失败的文件哨兵/流/stdout/WASM，成功→失败→成功，并发不同角色 | 结果隔离、无残留 PDF/临时文件，保留最终复制 I/O 限制 |
| C1-14 | 全嵌入/子集/禁止子集、Courier 与嵌入样式混用 | 文字/几何不变，真实字体/ToUnicode 正确，资源引用与 xref 有效 |

## 8. 独立检查与跨入口门槛

CLI、.NET、真实受保护浏览器 WASM 使用相同 HTML/Unicode、字体字节、角色顺序、页面与 subset 配置。同环境 PDF 应相等；跨系统采用明确平台记录并核对字体角色、文字、页数、几何，不只看一个哈希。旧 B2 基础证据保持历史事实，新角色必须在新最终 SHA 上真实运行；不将 Node 托管替代认作浏览器通过。

每份成功样张：qpdf；字体嵌入/子集由独立工具对照输入轮廓/advance/映射；ToUnicode 与人工预期文本块/标量/编号序列；bounding boxes；逐页打开原始 PNG。检查 PDF 无合成绘制，不能只看字体名称或黑像素。保存可查看 PDF，实际验证搜索/复制跨样式短语；文本提取与人工交互/页面检查分别记录，不能宣称已取得不存在的用户页面审批。

真实 browser-WASM 保留单线程、非应用 AOT、现有沙箱参数检查，不调用打印接口、不改变本地策略；本地受限时使用正常允许的受保护 CI并明确执行地点。Native AOT 仅按实际构建/运行平台报结果，WASM 编译不等于 AOT/WASM 正式发行。默认不增加运行时依赖。

最终门槛：同提交本地构建/测试与真实样张检查、独立代码审查与修复、正常 CI 通过、全部 PR 意见回复并解决、正常合并并核验 main。保持目标分母，不把 C1 明细当成新的完成项目；T-bold 只在新实现真实验证后更新，T-list 保持独立未完成。无数据库或包发布。

## 9. 核对源与首轮审查重点

[API](../../../src/FactsPDF/Api.cs)、[配置/输出边界](../../../src/FactsPDF/PdfConverter.cs)、[字体加载器](../../../src/FactsPDF/Fonts/PdfFont.cs)、[HTML](../../../src/FactsPDF/HtmlDocumentReader.cs)、[层叠](../../../src/FactsPDF/Css/CssDeclarations.cs)、[布局缓存/strut](../../../src/FactsPDF/TextLayout.cs)、[Unicode 序列化](../../../src/FactsPDF/UnicodePdfSerializer.cs)、[CLI](../../../src/FactsPDF.Cli/CliApplication.cs)、[私有 WASM](../../../probes/browser-wasm/ProbeConversion.cs)、[T-bold](../../acceptance/support-matrix.md)。

独立设计审查重点：四角色可独立提供及 Courier 混用是否闭合；无配对/自动发现/合成/降级；8 项与总字节统一；旧字体输入兼容；缓存/strut/实际字体测量；最终复制前后的输出保护边界；真实四角色 CJK 资源及跨端证据可取得性。用户给定方向明确，当前没有新增用户决策项；技术选择先按本契约与审查解决。
