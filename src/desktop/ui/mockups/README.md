# UI mockups

Place standalone HTML prototypes and their local assets in this directory. During UI development, Vite serves each HTML file directly:

```text
http://localhost:5173/mockups/<file-name>.html
```

Mockups are general references for visual character, hierarchy, density, and composition. They are not pixel-perfect specifications, do not override `AGENTS.md`, and do not authorize unavailable product behavior or decorative motifs that conflict with the production visual direction.

The canonical production guidance is in [`docs/product/ui-visual-direction.md`](../../../../docs/product/ui-visual-direction.md). These exported files are excluded from linting, formatting, and the production application entry point. Production React code belongs under `src`.
