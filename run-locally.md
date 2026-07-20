# Run ChangeLens locally

ChangeLens runs React in Vite, displays it in a Tauri window, and uses a local .NET engine. Tauri starts the engine as a child process and communicates with it through newline-delimited JSON on standard input and standard output. There is no backend HTTP port or Docker service to start.

## Prerequisites

| Dependency | Required version |
| --- | --- |
| Git | Available on `PATH` |
| Node.js and npm | Node.js 22.13.0 or later |
| .NET SDK | .NET 10 |
| rustup | Able to install the pinned Rust 1.97.1 toolchain |
| Native Tauri prerequisites | Required packages for the host operating system |

For Tauri's native dependencies:

- **macOS:** install the Xcode command-line tools with `xcode-select --install`.
- **Windows:** install Microsoft C++ Build Tools with the **Desktop development with C++** workload and ensure WebView2 is installed.
- **Linux:** install WebKitGTK 4.1 and the build packages for the distribution.

See the [official Tauri prerequisite instructions](https://v2.tauri.app/start/prerequisites/) for current platform-specific commands.

## First run and normal startup

Run the complete application from the repository root.

macOS or Linux:

```bash
cd src/desktop
npm ci
DOTNET_ENVIRONMENT=Development npm run desktop:dev
```

Windows PowerShell:

```powershell
Set-Location src/desktop
npm ci
$env:DOTNET_ENVIRONMENT = "Development"
npm run desktop:dev
```

`npm ci` is only required after cloning or when `package-lock.json` changes. On later runs, use only the environment setting and `npm run desktop:dev`.

The desktop command:

1. Builds `ChangeLens.Engine` in `Debug/net10.0`.
2. Starts Vite on `http://localhost:5173`.
3. Compiles and opens the Tauri window.
4. Starts the .NET engine and performs the `engine.getInfo` handshake.

Stop everything with `Ctrl+C`. Tauri also terminates its engine child process.

## Debug the complete application

`npm run desktop:dev` already uses a Debug build of the engine, Vite source maps and hot reload, and the Tauri development shell.

### .NET with Rider or Visual Studio

1. Open `src/engine/ChangeLens.slnx` and set breakpoints.
2. Start the complete application with `npm run desktop:dev`.
3. In Rider, select **Run → Attach to Process**. In Visual Studio, select **Debug → Attach to Process**.
4. Attach to the `dotnet` process whose command line contains `ChangeLens.Engine.dll`.
5. Reload the Tauri window after attaching if the initial `engine.getInfo` request has already completed.

Starting Tauri first and attaching afterward is the simplest workflow for request handling. Use Rider's **Attach to an Unstarted Process** only when a breakpoint must catch engine startup.

Tauri must own the integrated engine process because it owns the process's standard streams. An engine started independently from Rider or Visual Studio cannot connect to the current Tauri session.

### React UI with VS Code

Open the repository in VS Code and run `npm run desktop:dev` from its integrated terminal. Vite updates the Tauri window when React or TypeScript files change.

Inspect the real Tauri UI by right-clicking the window and selecting **Inspect**, pressing `Cmd+Option+I` on macOS, or pressing `Ctrl+Shift+I` on Windows or Linux.

## Run one layer at a time

### Browser-only React UI

```bash
cd src/desktop
npm run dev
```

Open `http://localhost:5173`. Hot reload and VS Code's **Debug: Open Link** workflow work normally, but the browser has no Tauri IPC bridge. `Desktop engine unavailable` is therefore expected.

### Tauri without rebuilding the engine

```bash
cd src/desktop
npm exec -- tauri dev
```

This skips the `predesktop:dev` .NET build hook, which is useful after building the engine in Rider or Visual Studio. Tauri still launches the existing engine DLL; it does not connect to an independently running engine.

### .NET engine alone

Run `ChangeLens.Engine` from Rider or Visual Studio to debug it independently. It waits for newline-delimited protocol requests on standard input and writes protocol responses to standard output. This process is not connected to Tauri.

## Logs and local configuration

`DOTNET_ENVIRONMENT=Development` enables the development settings and Debug-level engine logs. Engine diagnostics go to standard error so standard output remains reserved for protocol JSON.

Rolling log files are written to:

- macOS: `~/Library/Application Support/ChangeLens/Logs`
- Windows: `%LOCALAPPDATA%\ChangeLens\Logs`
- Linux: normally `~/.local/share/ChangeLens/Logs`

Override the directory with `ChangeLens__Logging__FileDirectory` and the log level with `Serilog__MinimumLevel__Default`. Do not put secrets in appsettings files, `VITE_` variables, or protocol messages.

## Verification commands

Engine:

```bash
dotnet test src/engine/ChangeLens.slnx --configuration Debug
```

React, from `src/desktop`:

```bash
npm run build
npm run typecheck
npm run lint
npm test
npm run format:check
```

Tauri, from `src/desktop/src-tauri`:

```bash
cargo test
cargo fmt --check
cargo clippy --all-targets --all-features -- -D warnings
```

## Troubleshooting

### `Desktop engine unavailable`

- Confirm `dotnet --version` selects .NET 10.
- Build `src/engine/ChangeLens.Engine/ChangeLens.Engine.csproj`.
- Start the integrated app with `npm run desktop:dev`, not browser-only `npm run dev`.
- Remove an incorrect `CHANGELENS_ENGINE_PATH` override.

### Port 5173 is already in use

Vite uses a strict port because Tauri loads `http://localhost:5173`. Stop the process occupying that port and restart the application.

### Rust or native-library build failure

Run Cargo from `src/desktop/src-tauri`, check `rustup show active-toolchain`, and confirm the operating system's Tauri prerequisites are installed.
