# ChangeLens UI visual direction

## Purpose and authority

This document defines the production visual direction for ChangeLens. It translates the general character of the HTML mockups into durable guidance without treating exported mockup details as pixel-perfect requirements.

Use visual sources in this order:

1. The durable repository rules in `AGENTS.md`.
2. This product visual-direction document.
3. Established production components and theme tokens.
4. The mockup screenshots and HTML as general composition and mood references.

Mockups do not authorize unavailable controls, navigation, data, or product behavior. When a mockup motif conflicts with a higher-priority source, omit that motif.

## Product character

ChangeLens should feel like a calm, precise engineering instrument:

- Clinical and focused rather than decorative.
- Code-native without rendering ordinary prose as code.
- Information-dense without becoming cramped.
- Structurally clear without turning every value into a card.
- Confident but visually restrained.

Avoid generic dashboard ornament, exaggerated elevation, oversized empty states, decorative gradients, and visual effects that do not communicate hierarchy or state.

## Theme behavior

ChangeLens supports explicit light and dark themes.

- The initial theme follows the operating-system preference.
- A visible application control lets the user switch between light and dark themes.
- An explicit user choice is stored locally as a non-sensitive interface preference.
- Both themes use the same hierarchy and semantic meanings; dark mode is not a separate visual design.
- Light mode uses a coherent light navigation rail rather than a permanently dark brand panel.

## Typography

- Use IBM Plex Sans for navigation, controls, explanations, headings, and behavioral narratives.
- Use IBM Plex Mono for paths, branches, revisions, symbols, file names, evidence references, and compact technical metadata.
- Use monospace uppercase labels sparingly for short structural labels.
- Prefer 32px/600 for major workspace headings and 20px/600 for panel headings.
- Do not use text smaller than 10px. Use at least 11px for technical metadata and explanatory notes when space permits.
- Secondary and tertiary text must remain readable against every surface on which it appears.

## Spacing, shapes, and elevation

Use a 4px micro-grid and an 8px primary spacing rhythm.

- Compact chips: 4px radius.
- Buttons, inputs, ordinary cards, and working panels: 8px radius.
- Dialogs and large temporary overlays: 12px radius.
- Read-only state pills may be fully rounded.
- Ordinary cards rely on surface tone and a fine full outline, not a shadow.
- Dropdowns, dialogs, and other temporary overlays may use subtle elevation.

Do not use single-edge borders or inset edge shadows as decoration or as active-state indicators. Communicate selection and emphasis through a full outline, background treatment, typography, and icon color.

## Color semantics

Color has a stable meaning:

- Blue: primary actions, focus, active controls, and selection.
- Teal: verified facts, current repository evidence, and successful deterministic outcomes.
- Violet: AI inference or conclusions that depend on reasoning.
- Amber: warnings and meaningful review concerns.
- Red: errors and critical findings.
- Slate: unknown or unavailable evidence.

Do not use teal merely because a user selected an item. Do not use warning or critical colors decoratively. Pair semantic color with text or an icon so meaning never depends on color alone.

## Surfaces and composition

- Use a cool application canvas, a slightly differentiated grouped surface, and an elevated working surface.
- Prefer one clear containing panel over several nested cards.
- Group related facts with spacing and separators before creating individual metric cards.
- Keep repository identity and revision data visually technical and compact.
- Show one visually dominant primary action per screen region.
- Consolidate repeated privacy assurances while keeping local-only behavior clear at the point where it matters.

## Layout modes

Use the layout that matches the task instead of forcing every screen into one padded dashboard shell:

- Setup and overview screens use a navigation rail, technical top bar, and comfortable workspace margins.
- Dense code and diff workspaces may use edge-to-edge split panes with a compact header.
- Long-running analysis may use a focused progress workspace with a secondary discovered-facts region.

Responsive layouts must preserve the task hierarchy. Stack secondary regions before compressing headings, paths, or primary controls beyond comfortable reading widths.

## Interaction and state

- Preserve semantic HTML, keyboard navigation, visible focus, and reduced-motion behavior.
- Present loading, empty, error, selected, ready, stale, unknown, and current states explicitly.
- Avoid fake percentages, unreliable completion estimates, decorative terminal output, or vague activity metrics.
- Keep repository and model-derived content visibly distinct from interface prose.

## Validation

Agent-run verification follows the command-line boundaries in `AGENTS.md`. For a visible UI change, the user-facing manual checklist should cover:

- Light and dark themes.
- A normal desktop size, 1280×800, and 960×640.
- Long repository paths, branch names, and revisions.
- Keyboard navigation and visible focus.
- Loading, empty, error, selected, ready, stale, unknown, and current states affected by the change.
- Reduced-motion behavior when animation is present.

The goal is visual consistency and usability, not screenshot-level reproduction of a mockup.
