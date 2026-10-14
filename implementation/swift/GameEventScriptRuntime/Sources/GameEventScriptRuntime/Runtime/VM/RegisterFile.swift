// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

/// View of the register storage exclusively owned by one `GesVirtualMachine`.
///
/// Registers are mutated only by the VM, serially, and in place: each subscript access derives
/// the element address from the owner's current base pointer, so register operations neither
/// copy values nor pay Swift's per-access exclusivity and copy-on-write checks. Host callbacks
/// reach registers only through the owner's dedicated accessors during their synchronous
/// invocation.
///
/// Obtain a view through `GesVirtualMachine.registers` for each access; storage moves only in
/// `GesVirtualMachine.ensure`, so a view must not be kept across an operation that may grow it.
struct GesRegisterFile {
    let base: UnsafeMutablePointer<GesValue>
    let count: Int

    @inline(__always)
    subscript(index: Int) -> GesValue {
        unsafeAddress {
            precondition(UInt(bitPattern: index) < UInt(bitPattern: count), "Register index out of range")
            return UnsafePointer(base + index)
        }
        nonmutating unsafeMutableAddress {
            precondition(UInt(bitPattern: index) < UInt(bitPattern: count), "Register index out of range")
            return base + index
        }
    }
}
