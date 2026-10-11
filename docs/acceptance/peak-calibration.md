# Cold RSS calibration — observation, not an engine budget

The original `acceptance_measure._cold_child` calls `subprocess.Popen` with
`start_new_session=True`, then `os.wait4` on that direct child. That avoids
accumulating unrelated exited children, but does **not** establish an isolated
post-exec renderer peak. On the Linux environment below, the child's process
lifetime high-water value retains a floor inherited from its Python launcher.
`peak_scope` must be read literally as process-lifetime evidence, not engine-only
memory. No measurement or engine implementation was changed for this experiment.

## Actual control experiment, 2026-10-11 UTC

Separate checkout at `46efd6e41834b3f543e2664691c2a5c96c45ff5b`;
Debian 13, Linux 6.18.44 x86_64, five exposed virtual CPUs, Intel Xeon Platinum
8573C, `/proc/meminfo` MemTotal 18,440,136 KiB. This is a shared execution
environment, not approved reference hardware. Python 3.12 and a small GCC-built
native control were used, with the **unchanged** `_cold_child` function.

The control touches every 4096-byte page of a live allocation and reads both
`getrusage(RUSAGE_SELF)` and `/proc/self/status` before freeing it. Two fresh
Python launchers retain either 0 or 192 MiB, then run independent 0/16/128 MiB
controls. Raw child PID, wait4 scope, exit, elapsed and stdout are retained.

| Parent retained MiB | Child touched MiB | wait4 peak bytes | Child VmHWM KiB |
|---:|---:|---:|---:|
| 0 | 0 | 13,893,632 | 664 |
| 0 | 16 | 17,301,504 | 17,056 |
| 0 | 128 | 134,742,016 | 131,744 |
| 192 | 0 | 214,827,008 | 660 |
| 192 | 16 | 214,958,080 | 17,056 |
| 192 | 128 | 214,958,080 | 131,740 |

This demonstrates launcher sensitivity in this environment. It does not prove
the exact cause or magnitude of the old CI constant 79,122,432-byte floor, nor
measure FactsPDF's isolated engine peak. The controls' `VmHWM` is read while
their controlled allocation remains resident; it is not an external sampler's
guarantee of catching every peak in a fast CLI.

## Reproduce the calibration without changing the engine

Save this C control as `/tmp/rss-probe.c` and compile with
`cc -O2 /tmp/rss-probe.c -o /tmp/rss-probe`:

```c
#include <stdio.h>
#include <stdlib.h>
#include <sys/resource.h>
int main(int argc, char **argv) {
    size_t n = argc > 1 ? strtoull(argv[1], 0, 10)*1024*1024 : 0;
    volatile char *p = malloc(n ? n : 1);
    if (!p) return 1;
    for (size_t i=0; i<n; i+=4096) p[i]=1;
    struct rusage u; getrusage(RUSAGE_SELF, &u);
    printf("self_ru_maxrss_kib=%ld\n", u.ru_maxrss);
    FILE *f=fopen("/proc/self/status", "r"); char line[256];
    if (!f) return 2;
    while (fgets(line, sizeof line, f))
        if (line[0]=='V' && line[1]=='m') fputs(line, stdout);
    fclose(f); free((void *)p); return 0;
}
```

From the repository root, run the following separately for `parent_mib=0` and
`parent_mib=192`, choosing a fresh output directory each time:

```python
import json, sys
from pathlib import Path
sys.path.insert(0, 'scripts')
from acceptance_measure import _cold_child
parent_mib = 192  # repeat in a fresh Python process with 0
retained = bytearray(parent_mib * 1024 * 1024)
rows = []
for child_mib in (0, 16, 128):
    row = _cold_child(['/tmp/rss-probe', str(child_mib)],
                     Path(f'/tmp/rss-{parent_mib}-{child_mib}'), 10)
    row['child_touched_mib'] = child_mib
    row['probe_output'] = Path(row['stdout_path']).read_text()
    rows.append(row)
print(json.dumps({'parent_retained_mib': parent_mib, 'rows': rows}, indent=2))
```

Next calibration should repeat these controls and actual B00/B01/B02-100 on
the chosen reference machine, with low- and high-RSS launchers and a minimal
native supervisor that stays small until fork/exec. Compare direct-child wait4
against independently sampled post-exec `/proc/<pid>/status`; explicitly report
sampling interval and missed short-lived processes. A supported isolated cgroup
peak experiment can be an additional process-tree measurement, with its own
scope; it is not interchangeable with engine allocations. Validate control
sensitivity and launcher invariance before proposing a replacement measurement.

Platform, exact machine, acceptable noise, absolute latency/RSS targets and
numeric regression budgets still require user confirmation. Preserve current
raw values as provisional observations; do not subtract the launcher floor,
impute a peak, fabricate a budget, or optimize the engine on this evidence.
