# ChangeLens

> Evidence-backed change intelligence for understanding software changes before they ship.

ChangeLens is a local desktop application that combines deterministic repository analysis with optional AI reasoning. It explains what a change does, reveals its wider impact, and highlights risks with inspectable evidence—so developers can move quickly without losing their understanding of the system.

## Initial focus

The first release is read-only: analyze a local Git change, map its affected areas, collect available engineering evidence, and present a concise, traceable review. AI findings are validated against the repository and clearly distinguished from hypotheses.

## Architecture

- **Desktop:** Tauri 2, React, TypeScript, Vite, and Monaco Editor
- **Engine:** self-contained .NET 10 process using a versioned standard input/output protocol
- **Local evidence:** Git CLI, SQLite, and a first deep .NET adapter built with Roslyn and MSBuild
- **AI:** an optional, provider-neutral reasoning capability with explicitly selected and filtered context

Repository access and engineering-tool execution stay in the local engine; the desktop shell and UI remain thin.

## Status

ChangeLens is in early development. The product is intentionally focused on trustworthy explanation and review before any code-modification capabilities are considered.
