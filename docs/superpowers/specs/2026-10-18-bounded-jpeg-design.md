# D1：显式 JPEG 资源与最小图文输出契约（范围已确认）

日期：2026-10-18。基线：C2 已合并的 main `d1e9aa31dd54a244145dc3771b7bfa3a4fad3fa3`。

状态：2026-10-18 01:50 UTC 用户对已列明接口范围回复“loadimage 比较好”，确认该范围并将公共方法命名为 `PdfImage.LoadImage`。按输入字节识别格式，首版仅支持下述 baseline JPEG；其他格式明确报错，无 PNG 实现或自动下载。正常实现、测试、PR 评论处理、CI、转待审及合并均已获连续执行授权。后续段落保留设计时的论证历史；其中待确认状态由本条取代。设计与源 fixture 本身不是功能验收。实施证据已由后续提交加入：LoadImage、显式 HTML/CLI/WASM 资源、真实图片 PDF 和跨入口检查；具体状态见实施计划与 PR #30。用户页面参考批准仍单独保持 pending，未上传 Library 或发布。

## 1. 范围与推荐

首个成功场景是单页中英报告中的一张静态 JPEG：显式资源、可预测尺寸、原比例、上下正文不重叠，以及明确的缺失/损坏/超额失败。复用已有字体和块布局；不新增运行时依赖。不是任意 JPEG 查看器或完整恶意图像沙箱。

比较三条资源入口：

| 方案 | 成本与边界 |
| --- | --- |
| **推荐：不可变 `PdfImage` + `PdfOptions.Images` 名称表** | 与显式字体方式一致；HTML 与二进制分离；调用方加载字节；CLI 只读显式参数路径；WASM 传明确字节。新增公共类型/属性和 CLI 参数，需要确认。 |
| HTML `data:image/jpeg;base64,...` | 输入自包含，但混合 HTML/二进制额度，产生编码副本，难以复用大资源；不作为本次默认入口。 |
| 资源解析回调或解码库 | 引入任意 I/O、生命周期/取消契约或运行时依赖，范围较大；不纳入 D1。 |

推荐格式为下述有界 baseline JFIF 子集。渐进 JPEG、CMYK、ICC 色彩管理和非正向 Exif 暂不支持；遇到这些有效但不支持的资源必须明确报错，不能默默当成普通 RGB。PNG、透明图、SVG、动画、自动本地路径解析及网络下载仍不在范围。

## 2. 拟议公共入口

```csharp
public sealed record PdfImageLimits
{
    public int MaxBytes { get; init; } = 8_388_608;
    public int MaxDimension { get; init; } = 8_192;
    public int MaxPixels { get; init; } = 16_777_216;
    public int MaxDecodedBytes { get; init; } = 50_331_648;
    public int MaxMcuBlocks { get; init; } = 1_048_576;
}
public sealed class PdfImage
{
    public int PixelWidth { get; }
    public int PixelHeight { get; }
    public int ByteLength { get; }
    public static PdfImage LoadImage(ReadOnlySpan<byte> source,
        PdfImageLimits? limits = null, CancellationToken cancellationToken = default);
    public static PdfImage LoadImage(Stream source,
        PdfImageLimits? limits = null, CancellationToken cancellationToken = default);
}
// Additions to PdfOptions (existing defaults and overloads stay compatible):
public IReadOnlyDictionary<string, PdfImage> Images { get; init; } // default empty
public int MaxImageResources { get; init; } = 16;
public int MaxTotalImageBytes { get; init; } = 33_554_432;
```

所有额度必须为正。乘积、MCU 向上取整、累计字节与偏移运算先用 checked 的宽整数；不依赖溢出后的数值做比较。默认额度是受控预览的工程上限，不是固定机器性能预算或查看器 RSS 保证。提高某项额度不会取消其他额度或 JPEG 自身 16-bit 尺寸限制。

`MaxDecodedBytes` 定义为 `width × height × componentCount` 的 8-bit 无 padding 样本字节数；`MaxMcuBlocks` 独立限制含边缘 padding 的 8×8 块数，不能用像素数替代。核心验证不做 IDCT、不分配整幅像素缓冲；PDF 查看器可能使用更多内存。长度上限在复制 span 或读取过量 stream 之前落实；stream 至多读取 limit+1 字节来判断超限，不要求 seek、不关闭调用方流。只有验证成功才返回拥有独立字节副本的不可变对象，外部后续修改输入数组不能改变输出。

资源名为 1–128 个 ASCII 字符，首位 `[A-Za-z0-9]`，其余 `[A-Za-z0-9._-]`，按 ordinal 大小写区分。它是名称而非 URI；不会 URL 解码、查文件、解析 data URI 或联网。转换开始时快照映射，最多枚举 `MaxImageResources+1` 个条目，拒绝 null 值和非法名称。验证所有提供的条目，包括未使用条目；每个条目都计入资源数和总压缩字节额度，即使多个名称指向同一对象。调用期间不得并发修改调用方集合，与现有字体集合契约相同。

只嵌入实际使用资源；同一 `PdfImage` 实例被多处引用只产生一个 Image XObject。不同实例即使字节相同也无需按内容去重。对象编号按首次实际使用顺序，不能依赖 Dictionary 枚举顺序。原 JPEG 字节原样嵌入，包括允许的元数据；不承诺元数据清除或图像重编码。

拟议 CLI：`--image chart=/explicit/path/chart.jpg`，可重复最多 16 次。按第一个 `=` 分隔名称和路径，路径中可含后续 `=`；路径相对当前工作目录，不相对 HTML。拒绝重名、空名称/路径、`-` 资源路径、缺参数与超数；名称检查与核心相同。与字体一样，CLI 用流加载并扣除剩余总额度；不先 `ReadAllBytes` 无界读取。禁止输出路径与输入 HTML、任一字体或任一图片路径相同，保留既有路径比较规则、临时文件与覆盖行为，不声称全面阻止符号链接别名。

Node 探针只在现有 `scripts/node_cli_process.mjs` 的通用 shell=false argv 调用中传递显式图片参数，不新增公开 Node 接口。WASM 只扩展私有探针：调用方提供名称和 `Uint8Array`，JS 在打包前先核对数量/名称/单图长度/总额度；.NET 导出再次核对名称数组、打包字节、长度数组的对应关系和总和，再调用同一 `LoadImage`。不增加图片 URL/fetch 入口，不复用现有字体宿主的无界 `response.arrayBuffer()` 读法。CI 宿主从固定本地 fixture 提供测试字节，不是新的公开 JS SDK。调用方已经创建的数组和 interop 分配不能由 loader 追溯约束。私有协议形状/名称错误使用 `B2_INPUT_IMAGES`，JS 打包前额度错误用 `B2_RESOURCE_LIMIT`，直接 .NET 协议额度错误仍为 `FPDF1603`；均不返回 PDF。

## 3. 接受的 JPEG 子集

| 项目 | D1 拟议行为 |
| --- | --- |
| 编码 | SOI、单个 SOF0、8-bit 精度、Huffman 顺序 DCT；一条包含所有分量的完整扫描，SOS 为 Ss=0/Se=63/Ah=Al=0。 |
| 灰度/彩色 | JFIF 1.00–1.02；首段 APP0 JFIF，14-byte payload、零缩略图，units 仅 0/1/2；1 个 Y 分量（ID 1、1×1）或 3 个 Y/Cb/Cr 分量（ID 1/2/3），顺序一致。YCbCr 允许 4:4:4、4:2:2、4:2:0，Cb/Cr 均 1×1。 |
| 表 | 非零 8-bit DQT；baseline DC/AC Huffman 表，表引用必须存在，码长/前缀树/值域合法且无全 1 码；DQT ID 0..3，DHT ID 0..1。重复定义、其他采样/表形式作为不支持拒绝。 |
| 重启 | 可选 DRI，非零值按 MCU 间隔检查 RST0..RST7 循环；DRI=0 不允许 RST。无容错重同步。 |
| 色彩/密度 | 彩色解释为 JFIF YCbCr，灰度为 Y；APP14 Adobe、APP2 ICC、其他 APP 类型明确不支持。JFIF X/Y density 必须非零且相等；不根据 dpi 改变布局尺寸。DeviceRGB/Gray 输出不宣称色彩管理。 |
| 方向 | 无 Exif 或唯一 Exif APP1 的 IFD0 Orientation 缺失/1 接受；2..8 为不支持；非法值/类型/重复标签或越界字段为损坏。不会静默旋转、翻转或交换尺寸。 |
| 元数据 | COM 允许；Exif 只为主图方向读取有界 TIFF header/IFD0。其余字段保持不解释，不遍历 Exif 子 IFD/缩略图、不宣称验证其语义。IFD0 条目类型/计数/值范围须可验证；最多 256 项。所有 APP/COM payload 合计最多 65,536 字节。 |
| 封装终点 | 必须完整扫描后 EOI，EOI 后不允许任何字节/拼接图片。长度段最多 256 个；不支持多帧、多扫描、DNL、分层、算术、无损及扩展顺序编码。 |

顺序校验：SOI → 有界标记段（先核对长度/范围）→ 格式/表/元数据 → 读取 SOF 后立即检查尺寸、像素、样本字节与 MCU 块额度 → SOS 引用 → 熵流 → EOI/EOF。识别不支持的编码时可立即报不支持，不为了诊断继续解释它的压缩体；尺寸超额案例必须在读取其不匹配的熵数据前报额度错误。不能把“合法 JPEG 但超出本子集”都描述为文件损坏。

完整熵流验证必须按期望 MCU/分量/块数量遍历：有界 Huffman 解码；DC category 0..11；AC size 1..10、EOB/ZRL、零游程与 64 系数边界；读足振幅位；处理 FF00 stuffing；检查扫描末尾及重启边界的全 1 填充位、重启顺序和终点。不能靠保留 EOI 就让半截扫描通过，不能容忍缺表后使用隐式标准表。验证器不重建像素/做 IDCT，不承诺检出所有颜色异常或语义异常图片。

普通标记、扫描末端 EOI 和 restart 前的合法重复 `FF` marker fill 明确接受；有界跳过后必须遇到实际非 FF marker code。它与熵字节 `FF00` 不同，不能把 `FF...00` 当成合法 marker，只有 FF 直到 EOF 仍为截断。长 fill 仍消耗 MaxBytes，遍历中每最多 8KiB 检查取消。

取消在读流/字节遍历每最多 8KiB、元数据每段/每 64 个 IFD 条目、熵流每最多 256 个块（含进入扫描前）检查；转换快照、布局、写图片流每最多 16KiB 也检查。状态全部为单次加载/转换局部，成功→失败→成功不能污染共享表。

## 4. HTML、尺寸与分页

`<img src="chart" alt="季度趋势" style="width:200pt">` 或自闭合形式。作为**独立块叶子**使用，可在根、body 和普通 div/section/article 等现有容器内。D1 不支持段落/行内元素内的图片、列表中的图片（任意 ol/ul/li 祖先）、浮动或文字环绕；这些结构明确报错，不自动拆散段落或创建无标记列表项。此限制是待确认的首批范围，不宣称浏览器 img 的默认行内语义。

`src` 必需且唯一；`alt` 可选，只接受为元数据，不充当找不到资源时的文字回退、不宣称 Tagged PDF。保留 id/class/style/lang/title/data-* 通用属性与现有选择器；width/height HTML 属性、srcset 等拒绝。`</img>` 拒绝。资源绑定/几何等新图片诊断指向相关 img 起始标签；词法/CSS 错误保留原有码及更具体的源偏移。重复 src 与其他重复属性一样，在 CSS 预检使用的 tokenizer 中先报 `FPDF1101`，位置为第二个属性起点，不修改通用词法行为。

img 使用现有 box 样式：CSS width（pt/px/%）、padding、solid border、background。width 是图片内容宽度，相对于父内容宽度解析百分比；外宽加上 padding/border，沿用现有盒模型。缺省内容宽度是 `PixelWidth × 0.75pt`，即固定 96dpi；高度始终为 `contentWidth × PixelHeight / PixelWidth`。不增加独立 height、object-fit、transform 或自动缩小，width=0/过宽/非有限几何均报 `FPDF1302`。图片内无文字；继承文字样式不会影响像素或纵横比，颜色仍供现有 currentColor 边框使用。

左对齐，不用 text-align 移动图片。与普通容器相同，img 本身不新增 margin/break CSS；上下间距可用现有相邻段落、容器 padding，显式分页用既有 p/h 标签。输入中的直写文本在 img 前后分成普通匿名段落，不能落在图片上。

图像的整个外盒（含自身装饰）不可拆分。剩余高度不足时整体移到下一页；连同必须在首内容前消费的祖先顶部 padding/border 仍放不下空页时报 `1302`，不循环新建空页。自身底 padding/border 一并保留，不能单独溢到下一页。祖先容器仍按已有 slice 规则分片；后续正文从图像外盒底部继续。即使无文字 runs，图像也让页面成为已占用，计入页数和物理边界，图片后显式分页不能产生幽灵页。

## 5. 序列化、错误与输出边界

`ImageNode` → 独立 `LayoutImage` 步骤 → `PlacedImage`。布局在保留每个 placed image 前收取一个 MaxDisplayCommands；自身/祖先的 box paint 继续按既有规则收费，不能绕过全局重计数。两个 serializer 共用二进制 Image XObject 写入和放置指令，ASCII 文档也可含图片，不通过 Base64/字符串承载 JPEG。

`BlockTreeTraversal.Paragraphs` 的既有文字投影须明确跳过 ImageNode，保留图片前后文字顺序，不能抛 Unexpected block tree child。图片和列表可作为兄弟出现在同一文档；这不改变“图片不能置于列表祖先内”的限制。

Image XObject：实际 Width/Height、BitsPerComponent=8、DeviceGray 或 DeviceRGB、DCTDecode；明确灰度 ColorTransform=0、YCbCr ColorTransform=1，禁止 APP14 的覆盖歧义。JPEG stream Length 等于实际压缩字节数。每页 /XObject 只列该页使用资源；q/cm/Do/Q 指令按手算矩阵放置，确认上下方向。背景/边框与图像的层叠必须在实际 PDF 检查，图像不能盖住自己边框或后续正文。保持旧无图片 PDF 的对象顺序与字节不变；Unicode 字体、ToUnicode、子集仍走原路径。

| 失败 | 拟议诊断/行为 |
| --- | --- |
| JPEG 损坏/截断/非法头表或熵流 | `FPDF1601`，加载阶段无 HTML offset |
| JPEG 类型、色彩、方向或元数据形式不支持 | `FPDF1602` |
| 单图/元数据/MCU/资源数量或总压缩字节超额 | `FPDF1603` |
| 合法 src 名称没有显式绑定 | `FPDF1604`，img 源偏移 |
| 缺失/非法 src、未知属性 | `FPDF1103`；结构/禁用位置 `1101`；重复属性沿用 tokenizer 的 `1101` 和第二属性起点 |
| CSS/尺寸/页数/显示命令/输出字节 | 原 `1201/1202`、`1302/1303`、`1401` |
| 非法 API options/名称/空资源对象 | ArgumentException 家族，沿用 API 误用分类 |
| CLI | 参数错误=2；上述 FPDF=3；文件 I/O=4；取消=130；stderr 诊断，stdout 不混入日志 |

映射快照/额度检查在 HTML 读取前；随后保留现有先 CSS 全文预检、再 HTML 结构/引用的顺序。loader 的格式/额度先后遵循 §3，不承诺收集所有错误。

核心继续先缓冲完整 PDF，最终复制前任何加载、解析、排版、输出超额失败不得改变调用方流内容/位置。最终复制阶段的 I/O 或取消仍可能产生部分流输出，不新增事务承诺。CLI 在正常文件路径下保留旧目标哨兵/不留下新目标/清理临时文件；stdout 只有完整构建后才开始复制。MaxOutputBytes 包括 JPEG 原字节、所有对象和 xref，不因复用/图片 MIME 类型豁免。

## 6. 已准备 fixture 与实施验收门槛

`tests/fixtures/jpeg/manifest.json` 和 `scripts/jpeg_fixtures.py`：独立生成的合成像素，无外部图片。manifest 的 expected_load 是本稿提案，不是引擎实测通过。Pillow 只用于测试资源作者/独立解码，不进入产品依赖。

正例覆盖 RGB 444/422/420、灰度、非整 MCU 边缘、restart、DQT/RST/EOI 前 marker fill、正向 Exif、等密度、COM。负例覆盖渐进/CMYK/旋转/ICC/Adobe/非方形像素、无 JFIF、错误头表、零量化、缺表、提前 EOI、截断段、错误 restart、越界 Exif、尾随数据及尺寸/像素/标记数量提前拒绝。过度分配 Huffman 样本保持段内符号数量完整，单独检验前缀树而不是提前触发段长度错误。部分独立查看器可能宽容解码负例；不能用其宽容行为改写本契约，也不能把所有不支持案例称为损坏。

单页提案 HTML 和运行场景见同目录 `report.html`、`acceptance-cases.json`。图片 320×160、CSS 200pt 给出独立预期 200×100pt（区别于自然尺寸 240×120pt）；四角非对称色块和向上箭头用于检查方向。逐例标明适用入口；四个自定义 PdfOptions 的负例只计 managed API，不假装 CLI/Node/WASM 已有页高或额度参数。跨入口额度验证另外使用真实默认限制的明确 recipe，并保留各层不同的预期诊断。它尚不是可用产品样例，不放入 examples 的已支持示例列表。

同一失败案例的 SourceOffset 断言仅适用于暴露 `FactsPdfException.SourceOffset` 的 managed API（JSON 的 assertion_entry_scope）；CLI 与现有私有 ProbeResult 没有位置字段，只核对它们实际输出的错误码/退出码与无输出行为，不为共享断言添加新的诊断接口。

实现时必须完成以下组，不将 fixture 自检当产品完成：

1. span/非 seek stream、精确上限及 +1、预取消/中途取消、输入副本不变、映射快照/重名/总额度/未用资源及成功→失败→成功。
2. 完整熵流验证的手工小位流：合法极值/DC/AC、EOB/ZRL、非法码/缺振幅/越界 run、stuffing、padding、restart 间隔和顺序。仅有编码器产生的文件不足以验证解析器。增加灰度/彩色 MCU 向上取整与 MaxDecodedBytes/MaxMcuBlocks 精确边界。
3. 宽度/自然尺寸/百分比/padding/border、单页上下文、图片独占页、剩余空间不足整体移页、过高拒绝、祖先装饰、图片后分页、只含图片的页、重复资源一次嵌入、未用资源不嵌入。单独严格验证不支持位置/属性。
4. .NET、CLI 文件/stdout、Node argv、Linux Native AOT 及 WASM 同一输入/资源同字节或可解释差异，所有适用负例保持无输出。WASM 先私有适配协议测试，再正常受保护 CI 实际浏览器运行，不绕过本地 PrintingEnabled 限制。
5. qpdf 结构；Poppler 文本/页面边界；pypdf 获取原始 DCT stream 与 fixture SHA 一致；PDF 独立渲染后检查真实像素方向、比例和正文，不只检查 /Width。双语字体复用已有授权 manifest；全部实际页逐页查看，用户参考审批单独保持 pending-review。
6. 原无图样例字节/旧测试、旧九目标分母及尚未完成项目保持真实。只有新实测通过后才更新 T-image 和支持文档。完整分支独立审查、正常同 head CI/PR 评论、合并后 main 核对；不动 Pages 配置、不发布、不自动上传 Library。

## 7. 已确认的产品接口包

用户已确认上述显式名称表、有界 limits、CLI `--image 名称=路径`，并指定公共方法使用 **`PdfImage.LoadImage`**。输入按内容识别；首版 baseline JFIF、方向仅 1、块级且列表外的范围保持。其他图片格式明确报 `FPDF1602`，空或截断 JPEG/损坏编码为 `FPDF1601`。此项新图像接口决策已解决，按计划连续实现、测试、审查并通过正常 PR/CI 门槛；不再重复询问。

独立设计审查状态：两轮复核通过，无剩余设计阻塞。首轮三项 P2（重复 src 的既有诊断、Huffman 负例隔离性、入口适用性）及一项 P3（合法 marker fill）均已修订，并完成定向复核；文字投影、Node 实际路径及 WASM 字节入口边界也已补齐。复跑确认 35 个 fixture、10 个正例独立解码通过；这些不是 JPEG 引擎或跨入口通过证据。本节的产品接口包随后已按用户 LoadImage 指示确认。实施计划：[2026-10-18-bounded-jpeg.md](../plans/2026-10-18-bounded-jpeg.md)。

## 来源与标准边界

既定范围：[首个可用版本设计](2026-10-11-first-usable-release-scope-design.md) §3/7/8。现有入口：`src/FactsPDF/Api.cs`、`Fonts/PdfFont.cs`、`PdfConverter.cs`、`HtmlDocumentReader.cs`、`BlockLayout.cs`、`TextLayout.cs`、两个 serializer、`src/FactsPDF.Cli/CliApplication.cs` 和私有 WASM 探针。

格式依据：[ITU T.81](https://www.w3.org/Graphics/JPEG/itu-t81.pdf) Annex B/C/E/F；[JFIF 1.02](https://www.w3.org/Graphics/JPEG/jfif3.pdf) 的分量、密度及 APP0；[CIPA Exif 2.3](https://www.cipa.jp/std/documents/e/DC-008-2012_E.pdf) 的 Orientation（本环境已下载标准并读取对应段）；[Adobe PDF 1.4 Reference](https://opensource.adobe.com/dc-acrobat-sdk-docs/pdfstandards/pdfreference1.4.pdf) §3.3.7/§4.8 的 DCTDecode/Image XObject。PDF 可支持更多 JPEG 类型，本稿的限制来自 D1 工程范围，不声称是 PDF 标准限制。
