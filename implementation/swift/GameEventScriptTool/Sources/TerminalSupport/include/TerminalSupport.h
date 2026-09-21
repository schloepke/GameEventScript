// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
#ifndef GES_TERMINAL_SUPPORT_H
#define GES_TERMINAL_SUPPORT_H
#include <stdint.h>
void ges_terminal_initialize(void);
int ges_terminal_isatty(int fd);
int ges_terminal_begin(void);
void ges_terminal_end(void);
/* A byte, -1 at EOF, -2 on timeout, or -3 on an I/O error. */
int ges_terminal_read(int timeout_ms);
int ges_terminal_columns(void);
int ges_scalar_width(uint32_t scalar);
#endif
