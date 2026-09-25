// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { readFileSync } from 'node:fs';
import { defineConfig } from 'astro/config';
import starlight from '@astrojs/starlight';

const grammar = JSON.parse(readFileSync(new URL('../tools/editors/TextMate/GameEventScript.tmbundle/Syntaxes/GameEventScript.tmLanguage.json', import.meta.url)));
const assembler = JSON.parse(readFileSync(new URL('../tools/editors/TextMate/GameEventScriptAssembler.tmbundle/Syntaxes/GameEventScriptAssembler.tmLanguage.json', import.meta.url)));

export default defineConfig({
  site: 'https://gameeventscript.org',
  output: 'static',
  trailingSlash: 'always',
  outDir: '../artifacts/website/dist',
  cacheDir: '../artifacts/website/cache',
  publicDir: '../artifacts/website/public',
  vite: { build: { license: { fileName: 'third-party-licenses.md' } } },
  integrations: [starlight({
    title: 'Game Event Script',
    description: 'A portable, event-driven scripting language. Learn GES and embed it in C# or Swift.',
    favicon: '/favicon-pixel-duo.svg',
    logo: { src: './brand/pixel-duo/mark.svg', alt: '', replacesTitle: false },
    components: {
      Header: './src/components/DocsHeader.astro',
      Banner: './src/components/NoInlineBanner.astro',
      Footer: './src/components/DocsFooter.astro',
    },
    locales: { root: { label: 'English', lang: 'en' } },
    social: [{ icon: 'github', label: 'GitHub', href: 'https://github.com/schloepke/GameEventScript' }],
    customCss: ['./src/styles/docs.css'],
    expressiveCode: {
      shiki: {
        langs: [{ ...grammar, name: 'ges' }, { ...assembler, name: 'gesa' }],
        langAlias: { eventscript: 'ges', bnf: 'text' },
      },
    },
    sidebar: [
      { label: 'Home', link: '/' },
      { label: 'Start here', items: [
        { label: 'Documentation overview', slug: 'docs' },
        { label: 'Learn the language', slug: 'docs/learn/language' },
        { label: 'Embed in C#', slug: 'docs/learn/csharp' },
        { label: 'Embed in Swift', slug: 'docs/learn/swift' },
      ] },
      { label: 'Tools', items: [
        'docs/tools/downloads', 'docs/tools/csharp-cli', 'docs/tools/swift-cli',
        'docs/tools/csharp-highlighter', 'docs/tools/swift-highlighter',
        'docs/tools/editor-bundles',
      ] },
      { label: 'Native integration', items: [
        'docs/implementations/csharp', 'docs/integration/csharp-bridge',
        'api/csharp',
        'docs/implementations/swift', 'docs/integration/swift-bridge',
        'api/swift',
      ] },
      { label: 'Language and host reference', collapsed: true, items: [
        'docs/reference/language', 'docs/reference/semantics/text',
        'docs/reference/semantics/numbers', 'docs/reference/semantics/determinism',
        'docs/reference/host-runtime', 'docs/reference/public-api',
        'docs/reference/diagnostics', 'docs/reference/message-format',
        'docs/reference/syntax-highlighting',
      ] },
      { label: 'Program formats', collapsed: true, items: [
        'docs/reference/program-model', 'docs/reference/bytecode',
        'docs/reference/binary-format', 'docs/reference/assembler-format',
      ] },
      { label: 'Conformance', collapsed: true, items: [
        'docs/reference/conformance/markdown-format', 'docs/reference/conformance/runner',
        'docs/reference/conformance/environment', 'docs/reference/conformance/coverage',
        'docs/reference/conformance/cross-language-acceptance',
      ] },
      { label: 'Packages and releases', items: [
        'docs/distribution/packages', 'docs/distribution/csharp', 'docs/changelog',
      ] },
    ],
  })],
});
