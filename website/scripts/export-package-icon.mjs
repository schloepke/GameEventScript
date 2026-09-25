// Copyright 2026 Stephan Schlöpke
// SPDX-License-Identifier: Apache-2.0

import sharp from 'sharp';
import { fileURLToPath } from 'node:url';

// Commit the PNG so NuGet packing needs neither Node nor an SVG renderer.
const source = new URL('../brand/pixel-duo/mark.svg', import.meta.url);
const destination = new URL('../brand/pixel-duo/nuget-icon.png', import.meta.url);
await sharp(fileURLToPath(source), { density: 192 })
  .resize(128, 128)
  .png({ compressionLevel: 9 })
  .toFile(fileURLToPath(destination));
console.log('Exported the transparent 128×128 Pixel-Duo NuGet icon.');
