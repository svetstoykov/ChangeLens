# React and Frontend Guidelines

Read before writing or changing any React, TypeScript, or CSS code under `src/desktop/ui`.

## Visual direction

- Treat `src/desktop/ui/mockups` as directional references for visual character, hierarchy, density, and interaction patterns, not as pixel-perfect specifications.
- Prefer accessibility, actual product behavior, and consistency with the production visual system over literal mockup replication. Do not add unavailable navigation, controls, data, or capabilities solely to resemble a mockup.
- Aim for a clinical, calm, code-native interface that feels like a precise engineering instrument rather than a generic web dashboard.
- Use IBM Plex Sans for interface prose and IBM Plex Mono for repository-derived values, paths, revisions, symbols, evidence, and compact technical metadata.
- Prefer cool layered surfaces, fine structural outlines, restrained radii, and minimal elevation. Reserve prominent shadows for temporary overlays.
- Use semantic colors consistently: blue for actions and selection, teal for verified facts, violet for inference, amber for warnings, red for critical findings, and slate for unknown information.
- Do not use single-edge borders or inset edge shadows as decorative emphasis or active-state indicators. Prefer spacing, color, typography, a full outline, or a background treatment.
- When a mockup conflicts with this file or `docs/product/ui-visual-direction.md`, follow this file first and the product visual-direction document second.

## Component design

- Prefer small, focused components with clear responsibilities.
- Use functional components and hooks.
- Keep state as local as possible; lift it only when multiple components need it.
- Avoid unnecessary global state. Use Context for genuinely shared application state.
- Derive values during rendering instead of storing duplicated state.
- Use `useEffect` only for synchronizing with external systems, not for ordinary calculations or event handling.
- Prefer composition over large components with many configuration props.
- Keep business logic outside UI components when it becomes reusable or difficult to follow.
- Use clear names for components, props, hooks, and event handlers.
- Use stable IDs for list keys; never use array indexes when items can change order.
- Avoid premature memoization. Add `useMemo`, `useCallback`, or `React.memo` only when there is a measured need.
- Keep forms controlled when validation or dynamic behaviour requires it.
- Reuse existing components and patterns before introducing new abstractions.
- Keep TypeScript types explicit at component boundaries; avoid `any`.

## Behavior and accessibility

- Preserve responsive behavior, keyboard access, visible focus, readable contrast, reduced-motion support, and explicit loading, empty, error, and success states.
- Handle loading, empty, error, and success states explicitly.
- Maintain accessibility: semantic HTML, labels, keyboard support, and appropriate ARIA attributes.

## Verification

Do not add React or frontend TypeScript test files, React testing libraries, browser test runners, test scripts, or frontend test configuration. Verify React changes with formatting, linting, type checking, and production builds, then provide a focused manual UI/UX checklist for the user.
