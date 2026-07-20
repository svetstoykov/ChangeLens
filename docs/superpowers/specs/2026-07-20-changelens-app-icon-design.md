# ChangeLens Application Icon Design

## Objective

Create a distinctive, legible application icon for ChangeLens that communicates change inspection without relying on text. The icon must satisfy Tauri's desktop build requirements and remain recognizable from a full-size application tile down to a small window or file icon.

## Visual Concept

The icon combines a magnifying lens with a compact diff symbol:

- A bold circular lens is the dominant silhouette.
- Two short horizontal change lines sit inside the lens.
- The upper addition line uses the interface success green, `#7ee2a8`.
- The lower removal line uses the interface error coral, `#ff9b9b`.
- The surrounding field uses the interface dark slate, based on `#111318`.
- A restrained cool-gray rim or highlight may separate the lens from the background.

The mark must not contain letters, words, fine code glyphs, gradients with strong banding, photorealistic materials, or decorative detail that disappears at small sizes. The lens and change lines should read through shape and color alone.

## Composition

The source is a square 1024 by 1024 pixel raster image. The mark is centered optically, with the lens handle extending toward the lower-right corner. All important geometry stays within a generous safe area so operating-system masks do not crop the mark.

The image should feel like a polished developer-tool icon: precise, modern, quiet, and trustworthy. It should use simple vector-friendly geometry, controlled depth, and crisp edges. Subtle lighting or dimensionality is acceptable only when it preserves clarity at 32 pixels.

## Deliverables

The canonical project asset is:

```text
src/desktop/src-tauri/icons/icon.png
```

The canonical asset is used as the input to Tauri's icon generation command. The generated icon directory must include the standard desktop outputs required by Tauri, including PNG sizes, macOS `icon.icns`, and Windows `icon.ico`. Generated mobile-specific assets may remain if the Tauri command creates them, but no separate mobile design is required.

## Generation and Processing

Generate one high-resolution master image from the approved concept. Inspect the result for composition, unwanted text, malformed symbols, edge artifacts, and color drift. If needed, make one focused revision that preserves the approved concept.

Copy the selected master into the repository as `icons/icon.png`, then run the project-local Tauri icon generator against that file. Do not install or depend on a separately versioned global Tauri CLI.

## Validation

Validation is complete when:

1. The canonical PNG exists, is square, and is at least 1024 by 1024 pixels.
2. The lens and both change lines are identifiable at 32 by 32 pixels.
3. The icon contains no unintended text, watermark, transparency fringe, or cropped geometry.
4. Tauri's icon generator completes successfully and produces the required desktop formats.
5. `cargo check --locked` proceeds past Tauri context generation without the previous missing-icon error.

No application behavior, UI layout, or product branding beyond the application icon changes as part of this work.
