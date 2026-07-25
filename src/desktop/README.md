# ChangeLens desktop development

The desktop workspace combines the React/Vite interface in `ui` with the Tauri shell in `src-tauri`. During integrated development, Tauri loads the Vite server and communicates with the real .NET engine through a narrow native command and the versioned standard-input/output protocol.

For full machine setup, configuration, Docker, ports, and troubleshooting, see
[`run-locally.md`](../../run-locally.md).

## Prerequisites

- Node.js 22.13 or later and npm
- .NET SDK 10
- Rust 1.97.1 through rustup
- The native prerequisites required by Tauri for your operating system

Install the JavaScript dependencies from this directory:

```bash
npm install
```

## Development commands

Run the React UI in a normal browser:

```bash
npm run dev
```

This mode provides Vite hot reload but has no native IPC bridge. Engine-backed actions are rejected and normalized into a safe `ActionError`; the UI presents the structured fallback without substituting mock data.

Run the complete React → Tauri → .NET development path:

```bash
npm run desktop:dev
```

The command builds `ChangeLens.Engine`, starts Vite, opens the Tauri window, launches the engine as a long-lived child process, and performs the real `engine.checkStatus` readiness action. React changes continue to hot reload inside the native window.

## Comparison setup flow

After readiness, open a repository through the native picker, select a local branch or cached remote-tracking target, prepare the comparison, and check freshness before relying on the displayed comparison facts. Remote-tracking refs are only cached local Git knowledge: ChangeLens never fetches or contacts a remote.

A green freshness result means the displayed aggregate facts still match the repository; it does not promise that unrelated repository data has not changed. Refresh is always explicit. Failed or timed-out actions are never replayed automatically, though a later user action can start a replacement Engine process.

Browser-only Vite has no native bridge and therefore displays safe desktop-boundary errors instead of mock repository or comparison data. Development continues to resolve the Engine from the local build until Phase 1D. Change context is transient UI state, is never logged or persisted, and no analysis action exists in this phase.

Other checks are available through `npm run build`, `npm run typecheck`, `npm run lint`, and `npm run format:check`.

## HTML mockups

Place standalone HTML prototypes and their local assets in `ui/mockups`. While `npm run dev` is running, open a prototype at:

```text
http://localhost:5173/mockups/<file-name>.html
```

Mockups are reference material and are not part of the production application entry point.
