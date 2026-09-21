// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
#define _XOPEN_SOURCE 700
#define _DEFAULT_SOURCE
#include "TerminalSupport.h"
#include <errno.h>
#include <locale.h>
#include <poll.h>
#include <signal.h>
#include <stdlib.h>
#include <sys/ioctl.h>
#include <termios.h>
#include <unistd.h>
#include <wchar.h>

static struct termios saved;
static volatile sig_atomic_t active;
static const int signals[] = { SIGINT, SIGTERM, SIGHUP, SIGQUIT };
static struct sigaction previous[4];

static void interrupted(int signal_number) {
    if (active) {
        const char reset[] = "\033[?2004l";
        (void)write(STDERR_FILENO, reset, sizeof(reset) - 1);
        tcsetattr(STDIN_FILENO, TCSANOW, &saved);
    }
    _exit(128 + signal_number);
}
void ges_terminal_initialize(void) {
    setlocale(LC_CTYPE, "");
    /* GUI launchers and LANG=C still edit UTF-8, so wcwidth needs a Unicode locale. */
    if (MB_CUR_MAX < 4 && !setlocale(LC_CTYPE, "C.UTF-8")) setlocale(LC_CTYPE, "en_US.UTF-8");
    signal(SIGPIPE, SIG_IGN);
    atexit(ges_terminal_end);
}
int ges_terminal_isatty(int fd) { return isatty(fd); }
int ges_terminal_begin(void) {
    if (active || tcgetattr(STDIN_FILENO, &saved) < 0) return 0;
    struct termios raw = saved;
    cfmakeraw(&raw);
    raw.c_oflag = saved.c_oflag;
    raw.c_cc[VMIN] = 1;
    raw.c_cc[VTIME] = 0;
    if (tcsetattr(STDIN_FILENO, TCSANOW, &raw) < 0) return 0;
    struct sigaction action = {0};
    action.sa_handler = interrupted;
    sigemptyset(&action.sa_mask);
    active = 1;
    for (int i = 0; i < 4; i++) sigaction(signals[i], &action, &previous[i]);
    return 1;
}
void ges_terminal_end(void) {
    if (!active) return;
    tcsetattr(STDIN_FILENO, TCSANOW, &saved);
    for (int i = 0; i < 4; i++) sigaction(signals[i], &previous[i], NULL);
    active = 0;
}
int ges_terminal_read(int timeout_ms) {
    struct pollfd descriptor = { STDIN_FILENO, POLLIN, 0 };
    int result;
    do { result = poll(&descriptor, 1, timeout_ms); } while (result < 0 && errno == EINTR);
    if (result == 0) return -2;
    if (result < 0) return -3;
    unsigned char byte;
    ssize_t count;
    do { count = read(STDIN_FILENO, &byte, 1); } while (count < 0 && errno == EINTR);
    return count == 1 ? byte : count == 0 ? -1 : -3;
}
int ges_terminal_columns(void) {
    struct winsize size;
    return ioctl(STDERR_FILENO, TIOCGWINSZ, &size) == 0 && size.ws_col > 0 ? size.ws_col : 80;
}
int ges_scalar_width(uint32_t scalar) { return wcwidth((wchar_t)scalar); }
