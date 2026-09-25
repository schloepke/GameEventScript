// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import { defineCollection } from 'astro:content';
import { glob } from 'astro/loaders';
import { docsSchema } from '@astrojs/starlight/schema';

export const collections = {
  docs: defineCollection({
    loader: glob({
      pattern: '**/*.md',
      base: '../artifacts/website/content',
      generateId: ({ entry }) => entry.replace(/\.md$/, '').replace(/\/index$/, ''),
    }),
    schema: docsSchema(),
  }),
};
