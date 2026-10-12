// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

protocol GesComponentIterator: AnyObject {
    func moveNext() -> Bool

    func component(_ index: Int) -> GesValue
}
