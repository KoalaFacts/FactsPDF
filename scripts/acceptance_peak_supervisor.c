/* Linux-only acceptance tooling, not a FactsPDF runtime dependency.
 * Fork only after exec has replaced the large Python launcher's address space.
 * Never create another session: Python owns the whole timeout process group.
 */
#define _DEFAULT_SOURCE
#include <errno.h>
#include <fcntl.h>
#include <signal.h>
#include <stdio.h>
#include <string.h>
#include <sys/resource.h>
#include <sys/wait.h>
#include <unistd.h>

int main(int argc, char **argv) {
    if (argc == 2 && strcmp(argv[1], "--version") == 0) {
        puts("FactsPDF wait4 supervisor v1");
        return 0;
    }
    if (argc < 5 || strcmp(argv[1], "--usage") || strcmp(argv[3], "--")) {
        fputs("usage: supervisor --usage NEW_FILE -- COMMAND [ARG...]\n", stderr);
        return 125;
    }
    int report = open(argv[2], O_WRONLY | O_CREAT | O_EXCL | O_CLOEXEC, 0600);
    if (report < 0) { perror("peak supervisor report"); return 125; }
    pid_t child = fork();
    if (child < 0) { perror("peak supervisor fork"); close(report); return 125; }
    if (child == 0) {
        close(report);
        execvp(argv[4], &argv[4]);
        int failed = errno;
        perror("peak supervisor exec");
        _exit(failed == ENOENT ? 127 : 126);
    }
    int status;
    struct rusage usage;
    pid_t waited;
    do { waited = wait4(child, &status, 0, &usage); } while (waited < 0 && errno == EINTR);
    if (waited < 0) {
        perror("peak supervisor wait4");
        kill(child, SIGKILL);
        while (waitpid(child, NULL, 0) < 0 && errno == EINTR) {}
        close(report);
        return 125;
    }
    int result = WIFEXITED(status) ? WEXITSTATUS(status) :
                 WIFSIGNALED(status) ? 128 + WTERMSIG(status) : 125;
    /* Linux reports KiB. A zero remains unknown to the Python collector. */
    int written = dprintf(report, "peak_rss_kib=%ld\nexit_code=%d\n", usage.ru_maxrss, result);
    int closed = close(report);
    if (written < 0 || closed < 0) { perror("peak supervisor report"); return 125; }
    return result;
}
