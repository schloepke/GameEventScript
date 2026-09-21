// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
#ifndef GES_CONFORMANCE_INSTRUMENTATION_H
#define GES_CONFORMANCE_INSTRUMENTATION_H
#include <stdint.h>
#include <stdbool.h>
typedef struct { uint64_t bytes; uint64_t allocations; bool valid; } ges_allocation_sample;
bool ges_allocation_install(void);
bool ges_allocation_begin(void);
ges_allocation_sample ges_allocation_end(void);
uint64_t ges_monotonic_nanoseconds(void);
bool ges_allocation_controls(void);
#endif
