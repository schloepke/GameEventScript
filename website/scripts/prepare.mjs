// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { mkdir, readFile, readdir, rm, writeFile } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { execFileSync } from 'node:child_process';
import { unified } from 'unified';
import remarkParse from 'remark-parse';
import remarkGfm from 'remark-gfm';
import remarkStringify from 'remark-stringify';
import { visit } from 'unist-util-visit';

const root = fileURLToPath(new URL('../', import.meta.url));
const repository = path.resolve(root, '..');
const output = path.join(repository, 'artifacts/website/content');
const publicOutput = path.join(repository, 'artifacts/website/public');
const github = 'https://github.com/schloepke/GameEventScript';
const documents = new Map([
  ['docs/README.md', 'docs/index'],
  ['docs/guide/README.md', 'docs/guides'],
  ['docs/guide/Language.md', 'docs/learn/language'],
  ['docs/guide/CSharp.md', 'docs/learn/csharp'],
  ['docs/guide/Swift.md', 'docs/learn/swift'],
  ['docs/guide/api/CSharp.md', 'api/csharp'],
  ['docs/guide/api/Swift.md', 'api/swift'],
  ['docs/guide/distribution/Packages.md', 'docs/distribution/packages'],
  ['docs/guide/distribution/CSharp.md', 'docs/distribution/csharp'],
  ['implementation/csharp/README.md', 'docs/implementations/csharp'],
  ['implementation/swift/README.md', 'docs/implementations/swift'],
  ['implementation/csharp/GameEventScript.Tool/README.md', 'docs/tools/csharp-cli'],
  ['implementation/swift/GameEventScriptTool/README.md', 'docs/tools/swift-cli'],
  ['implementation/csharp/GameEventScript.CSharpBridge/README.md', 'docs/integration/csharp-bridge'],
  ['implementation/swift/GameEventScriptSwiftBridge/README.md', 'docs/integration/swift-bridge'],
  ['implementation/csharp/GameEventScript.SyntaxHighlighter/README.md', 'docs/tools/csharp-highlighter'],
  ['implementation/swift/GameEventScriptSyntaxHighlighter/README.md', 'docs/tools/swift-highlighter'],
  ['CHANGELOG.md', 'docs/changelog'],
  ['tools/editors/README.md', 'docs/tools/editor-bundles'],
]);

function kebab(value) {
  return value.replace(/([a-z0-9])([A-Z])/g, '$1-$2').toLowerCase();
}

async function addSpecifications(directory) {
  for (const entry of await readdir(path.join(repository, directory), { withFileTypes: true })) {
    const source = `${directory}/${entry.name}`;
    if (entry.isDirectory()) await addSpecifications(source);
    else if (entry.name.endsWith('.md')) {
      documents.set(source, `docs/reference/${kebab(source.slice(6, -3))}`);
    }
  }
}

await addSpecifications('specs');
// Astro may retain hidden files when clearing its output. A production build
// starts from an empty script-owned directory; dev preview keeps the last build.
if (process.argv.includes('--clean-dist')) {
  await rm(path.join(repository, 'artifacts/website/dist'), { recursive: true, force: true });
}
await rm(output, { recursive: true, force: true });
await mkdir(output, { recursive: true });
await rm(publicOutput, { recursive: true, force: true });
const copyAssets = (source, destination) => execFileSync('python3', [path.join(repository, 'scripts/website_assets.py'), 'copy', source, destination], { stdio: 'inherit' });
copyAssets(path.join(root, 'public'), publicOutput);
execFileSync('python3', [path.join(repository, 'scripts/stage-api-docs.py')], { stdio: 'inherit' });
execFileSync('python3', [path.join(root, 'scripts/package-editor-bundles.py')], { stdio: 'inherit' });
execFileSync('python3', [path.join(root, 'scripts/package-brand.py'), '--site'], { stdio: 'inherit' });
// Pagefind writes assets outside Vite's client bundle; retain its MIT notice too.
copyAssets(path.join(root, 'node_modules/pagefind/LICENSE'), path.join(publicOutput, 'pagefind-licenses/'));
const markdown = unified().use(remarkParse).use(remarkGfm).use(remarkStringify, { fences: true });
for (const [source, destination] of documents) {
  const input = await readFile(path.join(repository, source), 'utf8');
  const titleMatch = input.match(/^# (.+)$/m);
  if (!titleMatch) throw new Error(`Missing document title: ${source}`);
  // Starlight owns the page heading. The canonical Markdown stays unchanged.
  const tree = markdown.parse(input.replace(titleMatch[0], ''));
  visit(tree, (node) => {
    // Canonical Markdown remains readable on GitHub; the site presents these
    // informative annotations as quiet inline notes rather than warning boxes.
    const versionLabel = node.type === 'blockquote' && node.children.length === 1
      && node.children[0].type === 'paragraph' && node.children[0].children.length === 1
      && node.children[0].children[0].type === 'strong'
      ? node.children[0].children[0].children : undefined;
    if (versionLabel?.length === 1 && versionLabel[0].type === 'text'
        && /^Since: (?:\d+\.\d+\.\d+|Unreleased)(?: — .+)?$/.test(versionLabel[0].value)) {
      const label = versionLabel[0].value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
      node.type = 'html';
      node.value = `<p class="ges-since"><strong>${label}</strong></p>`;
      delete node.children;
      return;
    }
    if (node.type === 'code' && node.lang === 'mermaid') {
      if (!/^\s*accTitle:\s*\S/m.test(node.value) || !/^\s*accDescr:\s*\S/m.test(node.value)) {
        throw new Error(`Mermaid diagrams require accTitle and accDescr: ${source}`);
      }
      const escaped = node.value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
      node.type = 'html';
      node.value = `<figure class="ges-diagram"><div class="ges-diagram-image" hidden></div><details open class="ges-diagram-source"><summary>Diagram source (Mermaid)</summary><pre><code>${escaped}</code></pre></details></figure>`;
      delete node.lang;
      delete node.meta;
      return;
    }
    if (!['link', 'image', 'definition'].includes(node.type)) return;
    if (node.url.startsWith('https://gameeventscript.org/')) {
      node.url = node.url.slice('https://gameeventscript.org'.length);
      return;
    }
    if (/^(?:[a-z][a-z\d+.-]*:|\/|#)/i.test(node.url)) return;
    const [relative, fragment] = node.url.split('#', 2);
    const resolved = path.posix.normalize(path.posix.join(path.posix.dirname(source), decodeURIComponent(relative)));
    const mapped = documents.get(resolved);
    node.url = mapped
      ? `/${mapped.replace(/\/index$/, '')}/${fragment ? `#${fragment}` : ''}`
      : `${github}/blob/main/${resolved.split('/').map(encodeURIComponent).join('/')}${fragment ? `#${fragment}` : ''}`;
  });
  const frontmatter = {
    title: titleMatch[1],
    description: `${titleMatch[1]} — Game Event Script documentation.`,
    editUrl: `${github}/edit/main/${source}`,
    banner: { content: 'Development documentation. Check the <a href="/docs/changelog/">changelog</a> when using a published package.' },
  };
  const target = path.join(output, destination + '.md');
  await mkdir(path.dirname(target), { recursive: true });
  await writeFile(target, `---\n${Object.entries(frontmatter).map(([key, value]) => `${key}: ${JSON.stringify(value)}`).join('\n')}\n---\n\n${markdown.stringify(tree)}`);
}
console.log(`Prepared ${documents.size} pages from canonical repository Markdown.`);
