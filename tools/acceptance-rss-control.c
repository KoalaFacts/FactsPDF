#define _POSIX_C_SOURCE 200809L
#include <stdio.h>
#include <stdlib.h>
#include <string.h>
#include <unistd.h>
#include <sys/resource.h>
int main(int argc, char **argv) {
    if (argc != 2) return 2;
    char *end; unsigned long m = strtoul(argv[1], &end, 10);
    if (*end || m > 128) return 2;
    size_t size = m*1024UL*1024UL;
    volatile unsigned char *p = malloc(size ? size : 1);
    if (!p) return 3;
    long page = sysconf(_SC_PAGESIZE);
    if (page <= 0) return 4;
    for (size_t i=0; i<size; i+=(size_t)page) p[i] = (unsigned char)i;
    FILE *f = fopen("/proc/self/status", "r");
    if (!f) return 5;
    char line[256]; unsigned long hwm=0;
    while (fgets(line,sizeof(line),f)) if (sscanf(line,"VmHWM: %lu kB",&hwm)==1) break;
    fclose(f);
    printf("{\"vm_hwm_bytes\":%lu,\"touched_mib\":%lu}\n",hwm*1024UL,m);
    free((void*)p);return hwm ? 0 : 6;
}
