<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Documentation website

The English site combines a single-page introduction with searchable
documentation. Astro and Starlight generate ordinary HTML, CSS and JavaScript
for existing static webhosting at `https://gameeventscript.org`.
No Node.js process, database or server-side application is required in production.

## Build and preview

Use Node.js 22.12 or newer, npm, Python 3, .NET 10 and Swift with DocC
(Xcode on macOS). Build the native API references before the website:

```sh
python3 scripts/build-api-docs.py --language csharp
python3 scripts/build-api-docs.py --language swift
cd website
npm ci
npm run build
npm run preview
```

The build reads the canonical Markdown, renders the website, creates the local
search index and verifies internal links, fragments and assets. The terminal
prints the preview URL. `npm run dev` starts a development preview instead.
After editing canonical Markdown outside `website`, restart that development
command to regenerate the imported pages. API source/build-input changes require
regenerating both native references; stale or missing outputs fail preparation.
The generators put libraries, symbols, caches, DocFX and HTML only below
`artifacts/website`. DocFX is pinned to 2.78.5 and installed locally on first use;
DocC comes from the installed Swift toolchain. No DocC plugin is added to the
public SwiftPM dependency graph.

Run `python3 scripts/test-guide-examples.py` from the repository root to compile
and execute the C#, Swift and GES tutorial snippets against this checkout.
`--language csharp` or `--language swift` selects one platform. These checks need
the corresponding .NET/Swift toolchain and may download build dependencies.

The uploadable output is **`artifacts/website/dist/`** at the repository root.
Generated content and build caches also stay under `artifacts/website`.
`scripts/clean.sh --artifacts-only` removes them as part of normal cleanup.

## Authoring

The Pixel-Duo master, usage guide, board template and shared CSS tokens live in
`brand/pixel-duo`. Both the landing page and documentation use that palette and
logo. The public build exports only `/brand/logo.svg` and `/favicon-pixel-duo.svg`;
`check-links.py` also enforces this publication boundary. Drafts and comparisons
are kept under `brand/drafts`, never in `public`.

To export the private brand board, all SVG variants and ZIP locally, run
`python3 website/scripts/package-brand.py` from the repository root. These
exports go to `artifacts/website/brand/pixel-duo`, outside the deployment output.
The site build uses `--site` and does not generate the private package. The
artwork has no embedded font or external icon dependency.

- Edit guides in `docs/guide`, references in `specs`, and implementation guides
  in their owning module. Do not edit generated content.
- Use `> **Since: 0.1.0**` or `> **Since: Unreleased**` below a title or beside a
  feature. The site renders these as subdued notes; GitHub retains a readable
  Markdown quote. For mixed sections, add ` — feature name` inside the bold
  text. See the documentation index for the informative version-note convention.
- `scripts/prepare.mjs` maps those documents to website routes. Markdown links
  to included pages become internal website links. Other repository links point
  to GitHub; code blocks are left as code, not interpreted as links.
- The landing page is `src/pages/index.astro`. Documentation navigation and
  site settings are in `astro.config.mjs`.
- GES and GESA code blocks load the existing canonical TextMate grammars directly.
- Use fenced `mermaid` blocks in canonical Markdown for state machines,
  branching flows or interacting participants. Keep exact conditions and
  exceptional cases in adjacent prose or tables. Plain sentences and short lists
  remain preferable for simple instructions. Mermaid blocks also render on GitHub.
- Mermaid is bundled locally and loaded only on documentation pages containing
  diagrams. It uses strict security mode and follows the light/dark theme.
  Diagram source remains available in a disclosure; without JavaScript or after
  a rendering failure it remains expanded. Include `accTitle` and `accDescr` in
  every diagram. See [Mermaid usage](https://mermaid.js.org/config/usage).
  Small screens can scroll wide diagrams horizontally.
- The Tools section offers JSON and classic XML editor bundles. Preparation
  packages both `.tmbundle` directories and `LICENSE` from `tools/editors` into
  reproducible ZIP downloads; no generated archives are checked in. They follow
  the website's source revision rather than a separately released version.
- The site explicitly labels its documentation as development documentation.
  Keep new guide sections marked **Unreleased** until release preparation.
  There is no separately maintained copy of the language specification.

## Git-based deployment to existing hosting

The `Website` GitHub Actions workflow builds and checks pull requests. After a
push to `main`, it publishes the verified build to **`site`** in the same GitHub
repository. A manual run on `main` can also publish; runs on other branches only
build. The workflow uses GitHub's automatic `GITHUB_TOKEN` with `contents: write`
only in the publication job. No personal access token or webhosting credentials
need to be stored in GitHub.

The first successful publication creates `site` with an independent root commit.
The branch contains only the contents of `artifacts/website/dist/`, with
`index.html` at its root. Later publications add ordinary commits and remove
obsolete generated assets. There are no force pushes. Identical output creates
no new commit; each publication records its source revision in the commit message.
The script refuses to overwrite a pre-existing unmanaged `site` branch.

For initial setup:

1. Merge the reviewed website changes into `main` and let the `Website` workflow
   succeed. This creates the `site` branch automatically; do not create it from
   `main` manually.
2. In the webhosting control panel, select this GitHub repository and the `site`
   branch. The branch root is the document root; no subdirectory is needed.
3. Enable the provider's pull/update mechanism and HTTPS for gameeventscript.org.
   The provider's existing GitHub access remains configured on the hosting side.
4. Verify the homepage, a nested `/docs/learn/csharp/` URL and site search.

Keep `site` outside rules requiring human pull requests or checks before every
push: it is generated output written by Actions. Keep normal source protection
on `main`. If repository policy forbids Actions from writing repository content,
adjust that policy for this publication workflow. GitHub Pages is not required.
Do not edit or merge `site` into a source branch; edit the canonical Markdown or
website sources and submit an ordinary source PR instead.

Publication uses exactly the artifact produced by that run's successful build.
Failed builds do not update `site`. Main runs are serialized, and the publisher
skips historical revisions after `main` has advanced. It uses a separate Git
index, preserving the source checkout and staged changes. Its regression tests
use disposable local Git repositories and never publish to GitHub:

```sh
python3 scripts/test-publish-website.py
python3 scripts/test-website-assets.py
```

The hosting provider is responsible for applying a pulled revision to the live
document root. If it supports atomic deployments, enable that feature. To roll
back, prefer reverting the source change and allowing a new publication, which
keeps the deployment history compatible with normal pulls.

## Manual upload alternative

1. Build and preview the intended Git revision.
2. Upload the **contents** of `artifacts/website/dist/` to the domain's document
   root using your provider's file manager, SFTP or deployment mechanism. Include
   `_astro`, `pagefind`, the sitemap files and every generated page directory.
3. Configure the host to serve `index.html` in directories and the generated
   `404.html` for missing pages. This is a multi-page static site; do not rewrite
   every request to the homepage as an SPA fallback.
4. Enable HTTPS for `gameeventscript.org`, then verify `/`, `/docs/`, a nested
   guide URL and search directly on the host.

Prefer an atomic directory switch if your provider supports it; retain the
previous complete build for rollback. Only the generated `dist` contents are
public assets. Do not upload the checkout, source files, dependency directory or
build caches. Deployment credentials and provider configuration are not stored
in this repository. Local building or previewing does not publish; the Actions
publication job is the automated deployment boundary.

Source copying and downloadable bundles exclude `.DS_Store`, AppleDouble `._*`,
`__MACOSX`, `Thumbs.db` and `desktop.ini`. Production builds clear `dist` first,
including hidden files left by earlier builds, and remove OS metadata from the
generated output again before final validation (Finder can recreate it during a
build). Both the standalone final check and Git
publisher reject these files, symlinks and source-only directories. Legitimate
hidden assets such as `.well-known` remain permitted. If Finder or another file
manager has touched the output after building, run
`python3 website/scripts/check-links.py` again immediately before manual upload;
metadata created after validation must also be excluded by the upload client.

The website is currently intended for the domain root. Moving it under a URL
prefix requires updating the route/link mapping and testing that deployment.

## Dependencies

The lockfile pins the build tools. Vite emits `third-party-licenses.md` for the
bundled client dependencies; preparation also copies Pagefind's MIT license to
`pagefind-licenses/`. Keep both with the uploaded build. Dependencies are
third-party packages, not authored GES source. The site uses system fonts and
does not load analytics or remote font services.

## Generated API references

C# XML comments and public assemblies generate DocFX pages under
`/api/csharp/reference/`. SwiftPM extracts public symbol graphs from the root
distribution package, and DocC generates one static archive per public module
under `/api/swift/`. The main site supplies landing pages at `/api/csharp/` and
`/api/swift/`, so guides and API references have separate navigation.
Symbol extraction includes extension blocks so the bridge's additions to Runtime
and standard Swift types appear in its reference. Staging requires representative
Host, Message and Context extension pages as well as the four module pages.
Each reference shows **Unreleased** and its source commit. These are development
references; package version properties are not used to claim release availability.

The Website workflow builds native references in separate .NET/Linux and
Swift/macOS jobs. It downloads only artifacts from the same run, checks their
commit and source-input fingerprint, and publishes the combined site through the
existing `site` branch. Missing module output also fails the build.
`python3 scripts/test-api-docs.py` exercises these failure controls without native
toolchains. Generation and local builds never publish.

`.spi.yml` uses Swift Package Index's `external_links.documentation` to point
to our hosted Swift reference; SPI does not build a duplicate DocC site. The
root README's compatibility badges use the exact SPI-provided endpoints. The
manifest was checked with the official SPI validator. Merge and site deployment
are needed before the new reference links are publicly available.

Third-party API renderer licenses and notices are retained below each API
reference. Source copies live under `website/api/licenses`; retain upstream
notices unchanged.
