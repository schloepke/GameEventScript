// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0
#include "ConformanceInstrumentation.h"
#include <stddef.h>
#if defined(__APPLE__)
#include <dlfcn.h>
#include <malloc/malloc.h>
#include <stdlib.h>
#include <stdatomic.h>
#include <time.h>
#include <pthread.h>
// libmalloc's exported instrumentation callback; deliberately confined to this tool.
// ABI source: apple-oss-distributions/libmalloc, src/malloc.c.
typedef void (*ges_malloc_logger)(uint32_t, uintptr_t, uintptr_t, uintptr_t, uintptr_t, uint32_t);
static _Atomic(ges_malloc_logger) *ges_logger_slot;
typedef struct { bool active; ges_allocation_sample sample; } ges_thread_measurement;
static pthread_key_t ges_key;
static pthread_once_t ges_key_once = PTHREAD_ONCE_INIT;
static bool ges_key_valid;
static _Thread_local ges_thread_measurement ges_thread;
static void ges_create_key(void) { ges_key_valid = pthread_key_create(&ges_key, NULL) == 0; }
static void ges_log(uint32_t type, uintptr_t a, uintptr_t b, uintptr_t c, uintptr_t result, uint32_t skip) {
    (void)skip;
    ges_thread_measurement *state = pthread_getspecific(ges_key);
    if (!state || !state->active || !(type & 2) || !result) return;
    ges_allocation_sample *sample = &state->sample;
    // Zone allocation: zone, size. Reallocation: zone, old pointer, new size.
    uint64_t size = (type & 8) ? ((type & 4) ? c : b) : ((type & 4) ? b : a);
    if (UINT64_MAX - sample->bytes < size || sample->allocations == UINT64_MAX) sample->valid = false;
    else { sample->bytes += size; sample->allocations++; }
}
bool ges_allocation_install(void) {
    pthread_once(&ges_key_once, ges_create_key);
    if (!ges_key_valid) return false;
    ges_logger_slot = (_Atomic(ges_malloc_logger) *)dlsym(RTLD_DEFAULT, "malloc_logger");
    if (!ges_logger_slot) return false;
    ges_malloc_logger expected = NULL;
    return atomic_compare_exchange_strong(ges_logger_slot, &expected, ges_log) || expected == ges_log;
}
bool ges_allocation_begin(void) {
    if (!ges_logger_slot || atomic_load(ges_logger_slot) != ges_log || ges_thread.active) return false;
    ges_thread.sample = (ges_allocation_sample){ .valid = true };
    if (pthread_setspecific(ges_key, &ges_thread) != 0) return false;
    ges_thread.active = true;
    return true;
}
ges_allocation_sample ges_allocation_end(void) {
    bool was_active = ges_thread.active;
    ges_thread.active = false;
    ges_thread.sample.valid = ges_thread.sample.valid && was_active && ges_logger_slot && atomic_load(ges_logger_slot) == ges_log;
    pthread_setspecific(ges_key, NULL);
    return ges_thread.sample;
}
uint64_t ges_monotonic_nanoseconds(void) { return clock_gettime_nsec_np(CLOCK_UPTIME_RAW); }
// Indirect calls and escaped volatile pointers prevent dead-allocation elimination.
static void *volatile ges_escape;
bool ges_allocation_controls(void) {
    malloc_zone_t *zone = malloc_default_zone();
    if (!ges_allocation_begin()) return false;
    ges_allocation_sample empty = ges_allocation_end();
    if (!empty.valid || empty.bytes || empty.allocations) return false;
    for (int kind = 0; kind < 7; kind++) {
        if (!ges_allocation_begin()) return false;
        void *ptr = NULL;
        switch (kind) {
            case 0: ptr = malloc(137); break;
            case 1: ptr = calloc(3, 137); break;
            case 2: ptr = malloc(137); ges_escape = ptr; ptr = realloc(ptr, 521); break;
            case 3: ptr = malloc_zone_malloc(zone, 137); break;
            case 4: (void)posix_memalign(&ptr, 64, 256); break;
            case 5: ptr = valloc(137); break;
            default: ptr = aligned_alloc(64, 256); break;
        }
        ges_escape = ptr;
        free(ptr); // Freed within the interval must still be counted.
        ges_escape = NULL;
        ges_allocation_sample sample = ges_allocation_end();
        uint64_t expected = kind == 1 ? 411 : kind == 2 ? 658 : kind == 4 || kind == 6 ? 256 : 137;
        if (!sample.valid || sample.bytes != expected || sample.allocations != (kind == 2 ? 2 : 1)) return false;
    }
    return true;
}
#else
bool ges_allocation_install(void) { return false; }
bool ges_allocation_begin(void) { return false; }
ges_allocation_sample ges_allocation_end(void) { return (ges_allocation_sample){0}; }
uint64_t ges_monotonic_nanoseconds(void) { return 0; }
bool ges_allocation_controls(void) { return false; }
#endif
