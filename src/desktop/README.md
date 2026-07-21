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

The command builds `ChangeLens.Engine`, starts Vite, opens the Tauri window, launches the engine as a long-lived child process, and performs the real `engine.getInfo` handshake. React changes continue to hot reload inside the native window.

Other checks are available through `npm run build`, `npm test`, `npm run typecheck`, `npm run lint`, and `npm run format:check`.

## HTML mockups

Place standalone HTML prototypes and their local assets in `ui/mockups`. While `npm run dev` is running, open a prototype at:

```text
http://localhost:5173/mockups/<file-name>.html
```

Mockups are reference material and are not part of the production application entry point.
