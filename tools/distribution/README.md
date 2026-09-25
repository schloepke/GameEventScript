<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# CLI distribution resources

The build, native verification and release collector live in `scripts`.
[Distribution instructions](../../docs/guide/distribution/Tools.md) describe
supported targets, installation and publication.

`licenses` contains unmodified upstream notices for the pinned Swift 6.4.0
Static Linux SDK. `sources.json` records each source URL. The inventory includes
Swift, Foundation, Dispatch, LLVM runtime components, ICU 76.1, musl 1.2.5,
musl-fts 1.2.7, libxml2 2.14.5, curl 8.15.0, BoringSSL, zlib 1.3.1, bzip2,
XZ 5.8.1, libarchive 3.8.1 and mimalloc 2.2.4. Some optional SDK libraries are not
linked by this CLI; their notices are retained conservatively. The SDK's own
SPDX bill of materials is copied separately into each Linux archive.

Do not replace these third-party notices with project headers. When changing the
SDK or its toolchain, review and update the inventory together. C# runtime notices
are obtained from the exact runtime package selected by the .NET SDK at build time.
The current CLI package's terminal-dependency notices are copied separately.
