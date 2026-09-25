<!-- Copyright 2026 Stephan Schlöpke -->
<!-- SPDX-License-Identifier: Apache-2.0 -->

# Game Event Script — Pixel-Duo

Two speech forms exchange roles around a shared space. Their pixel tails and
small particles connect that conversation to games. The blue and teal voices
have equal visual weight; neither colour identifies a particular implementation.

## Logo system

- `mark.svg`: full-colour transparent master, at 32 px or larger.
- `nuget-icon.png`: transparent 128×128 export embedded in NuGet packages.
- `mark-small.svg`: removes detached particles for 16–24 px applications.
- `mark-mono.svg` and `mark-inverse.svg`: one-colour navy and white versions.
- `icon.svg`: full mark on a navy tile; `icon-small.svg` uses the small mark.
- `wordmark.svg` and `wordmark-inverse.svg`: horizontal logo and product name.
- `brand-board.svg`: the complete visual overview.
- `tokens.css`: reusable CSS colours, font stacks and shape tokens.

Keep the relative positions of the two voices. Do not rotate, stretch or add
outlines, eyes or shadows to the mark. Keep a clear area of at least 8 units
around its 64-unit canvas. Keep detached pixels away from neighbouring text.
Use the one-colour variants when gradients cannot be reproduced clearly.

The horizontal name reads **Game Event Script**. Code and package identifiers
retain **GameEventScript**. Do not abbreviate the visible wordmark to GES.

## Colour roles

| Role | Values | Use |
| --- | --- | --- |
| Player blue | `#4864D9` → `#79A7EF` | First voice, brand illustrations |
| Player teal | `#2B99A6` → `#78CCC1` | Second voice, supporting illustrations |
| Action blue | `#4564D6` | Primary actions and light-surface links |
| Action teal | `#247F8B` | Secondary actions and light-surface accents |
| Ink / surface / paper | `#10203C` / `#FFFFFF` / `#F3F5FA` | Text, cards and page backgrounds |
| Muted text | `#53627B` | Supporting text on white or paper |
| Dark-surface accents | `#A9C3FF` / `#91DED3` | Links and accents on ink |

Use gradients for graphics, not ordinary text or button backgrounds. White text
against action blue has a calculated contrast ratio of 5.19:1; against action
teal it is 4.68:1. Validate actual components, including focus and hover states,
when implementing a UI. Colour does not carry meaning without a label or shape.

In the documentation's dark theme, reading surfaces and side navigation use
neutral near-black and charcoal backgrounds. Navy is retained for the top
navigation and dedicated brand artwork; blue and teal remain accent colours.

## Typography and layout

Use the system sans-serif stack in `tokens.css`: bold headings (700–750),
medium navigation (600–650), and regular body text (400–450). Body line height
is 1.6–1.8. Code uses the system monospace stack. No font files are bundled.
SVG wordmarks contain live text: typography varies by installed system fonts.
For fixed production artwork, outline the text using the chosen licensed font.

Use generous space, soft card corners and crisp pixel accents. Keep particles
sparse and tied to a message or interaction; avoid decorating every surface.
Maintain a clear reading order, visible keyboard focus and reduced-motion
support if motion is introduced. The static brand assets do not animate.

## Voice

Friendly, precise and curious. Use concrete verbs such as **receive**, **decide**
and **emit**. Explain the host and message flow through small runnable examples.
The existing line **Game rules, driven by events.** connects the brand to the
product without promising unsupported functionality.

## Assets and source

The editable master, guide, tokens and board template live in
`website/brand/pixel-duo`. Run `python3 website/scripts/package-brand.py` from
the repository root to generate variants and `GameEventScript-Brand.zip` below
`artifacts/website/brand/pixel-duo`, outside the published site. Website
preparation uses `--site` to export only its logo and favicon. Drafts, this guide,
the board and the brand ZIP are not published.
Edit the master and rebuild rather than editing the exported variants.
After changing `mark.svg`, run `npm ci --prefix website` and
`node website/scripts/export-package-icon.mjs`, then commit the updated
`nuget-icon.png`. The checked-in PNG lets .NET pack without Node or an SVG
renderer; the package verification checks its inclusion and exact bytes.
The ZIP includes the repository's Apache-2.0 license. These original SVG shapes
have no external icon or font dependency. The package is not a trademark clearance.
