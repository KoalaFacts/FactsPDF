"""Executable F4 inputs. This generator supplies no PDF expectations or references."""
from pathlib import Path


def render(count):
    if count not in (10, 30, 100): raise ValueError('Use the three authored record counts')
    rows = []
    for i in range(1, count + 1):
        note = f'<br>NOTE-{i:03} 已复核 / reviewed' if i % 10 == 0 else ''
        rows.append(f'<tr><td>R{i:03}</td><td>通过 / Pass</td><td>检查 {i:03} / Check {i:03}{note}</td></tr>')
    return '''<!doctype html>
<html lang="zh"><head><meta charset="utf-8"><title>Combined quality report</title>
<style>
body{font-size:11pt;line-height:2;color:#183348}
p,h1,h2{margin-top:0;margin-bottom:0}
h1{font-size:16pt;line-height:2;padding-top:4pt}
h2{font-size:12pt;line-height:2;break-after:avoid}
.summary{padding:8pt;border:1pt solid #24445a;background-color:#eef4fa}
img{width:240pt;padding:4pt;border:1pt solid #24445a}
table{width:520pt;font-size:11pt;line-height:2}
th,td{padding:4pt;border:1pt solid #24445a;background-color:white}
th{background-color:#dce9f2;font-weight:bold}
</style></head><body>
<h1><strong>质量复核 / Quality review</strong></h1>
''' + f'<p>REPORT-{count:03} 合成记录 / Synthetic records</p>\n' + '''<section class="summary"><p>状态 <strong>通过</strong> / <em>Reviewed</em> / <strong><em>Priority</em></strong></p></section>
<ol><li>核对记录 / Check records<ul><li>保留证据 / Retain evidence</li></ul></li><li>签署结论 / Sign decision</li></ol>
<img src="quality-chart" alt="Synthetic orientation chart">
<p>CHART-01 合成示意 / Synthetic chart</p>
<h2><strong>记录明细 / Record details</strong></h2>
<p>逐行核对 / Review every row</p>
<table><colgroup><col style="width:80pt"><col style="width:160pt"><col style="width:280pt"></colgroup>
<thead><tr><th>ID</th><th>状态 / Status</th><th>说明 / Description</th></tr></thead><tbody>
''' + '\n'.join(rows) + '''
</tbody></table>
<h2><strong>复核结论 / Review decision</strong></h2>
<p>记录完整 / Records complete<br>''' + f'END-{count:03} 已核准 / Accepted' + '''</p>
</body></html>
'''


if __name__ == '__main__':
    root = Path(__file__).resolve().parents[1]
    for count in (10, 30, 100):
        (root / f'examples/combined-report-{count}.html').write_text(render(count))
