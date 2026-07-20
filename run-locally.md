# Run ChangeLens locally

This guide covers the current development build: React runs in Vite, Tauri opens the native window, and Tauri starts the .NET engine as a child process. The engine communicates only through newline-delimited JSON on standard input and standard output.

## What works today

The current vertical slice starts the complete UI → Tauri → engine process path and displays the engine name, version, and protocol version. The repository already contains Monaco and the three .NET project boundaries, but repository analysis, SQLite persistence, Roslyn/MSBuild analysis, and AI providers have not been implemented yet.

That distinction matters for setup:

- There is no message broker or server database to start.
- SQLite will be embedded in the engine process and will not use a network port or Docker container.
- Git and any SDK needed to inspect a target repository run on the host so ChangeLens can discover their real capabilities.
- Remote AI credentials and internet access will be optional. No AI provider is wired into the current slice.

## Prerequisites

Install these tools for ChangeLens development:

| Dependency | Required version | Why it is needed |
| --- | --- | --- |
| Git | A supported Git CLI available on `PATH` | Source checkout now; repository inspection later |
| Node.js | 22.13.0 or later, with npm | React, TypeScript, Vite, and the Tauri CLI |
| .NET SDK | .NET 10 SDK | Builds and runs `ChangeLens.Engine`; `global.json` keeps selection on .NET 10 |
| rustup | Any current rustup capable of installing Rust 1.97.1 | `src-tauri/rust-toolchain.toml` selects the exact Rust toolchain |
| Native Tauri prerequisites | Platform-specific | Compiles the desktop shell and supplies the system webview |

Docker with Compose v2 is optional today because [`compose.yaml`](compose.yaml) intentionally contains no services. Install it when a real external development dependency is added. Docker Desktop includes Docker Engine, the CLI, and Compose; Linux developers may instead install the Compose plugin with Docker Engine.

Check the command-line dependencies after installation:

```bash
git --version
node --version
npm --version
dotnet --version
rustup --version
docker compose version
```

The expected Node output is `v22.13.0` or newer. The selected .NET SDK must begin with `10.`, and running Cargo from `src/desktop/src-tauri` installs or selects Rust `1.97.1` automatically.

### Native Tauri prerequisites

- **macOS:** install the Xcode command-line tools with `xcode-select --install`. Full Xcode is only needed for iOS development.
- **Windows:** install Microsoft C++ Build Tools with the **Desktop development with C++** workload. Install the WebView2 Runtime if it is not already present.
- **Linux:** install WebKitGTK 4.1 and the build packages for your distribution. For Debian or Ubuntu, use the package command in the official Tauri prerequisites guide; package names differ across distributions.

See the [official Tauri prerequisite instructions](https://v2.tauri.app/start/prerequisites/) for current operating-system commands.

## First-time setup

Run commands from the repository root unless a step says otherwise.

Restore the .NET projects:

```bash
dotnet restore src/engine/ChangeLens.slnx
```

Install the locked JavaScript workspace dependencies:

```bash
cd src/desktop
npm ci
cd ../..
```

Verify the Compose definition. It resolves to an empty service set in the current slice, so there is nothing to start:

```bash
docker compose config
```

Do not put Git, SQLite, the .NET engine, Vite, or Tauri in Compose. They need host access or are part of the application itself.

## Run the complete desktop application

From `src/desktop`, select the .NET development environment and start Tauri.

macOS or Linux:

```bash
cd src/desktop
DOTNET_ENVIRONMENT=Development npm run desktop:dev
```

Windows PowerShell:

```powershell
Set-Location src/desktop
$env:DOTNET_ENVIRONMENT = "Development"
npm run desktop:dev
```

`npm run desktop:dev` performs the integrated startup sequence:

1. Builds `ChangeLens.Engine` in `Debug/net10.0`.
2. Starts the Vite development server on `http://localhost:5173`.
3. Compiles and opens the Tauri desktop window.
4. Starts the engine DLL with the host `dotnet` command.
5. Sends the real `engine.getInfo` request over the versioned standard-input/output protocol.

Stop the application with `Ctrl+C`. Tauri terminates the engine child process when the desktop process exits.

## Run one layer at a time

### Browser-only UI

```bash
cd src/desktop
npm run dev
```

Open `http://localhost:5173`. Vite hot reload works, but a browser has no Tauri IPC bridge; the page therefore reports `Desktop engine unavailable`. This is expected and no mock engine is substituted.

### Engine protocol

Build the engine, then send one protocol request from the repository root:

```bash
dotnet build src/engine/ChangeLens.Engine/ChangeLens.Engine.csproj --nologo
echo '{"protocolVersion":1,"requestId":"local-1","method":"engine.getInfo"}' | dotnet run --no-build --project src/engine/ChangeLens.Engine/ChangeLens.Engine.csproj
```

The engine writes one JSON response to standard output. Diagnostics must go to standard error because standard output is reserved for protocol messages.

### Tauri shell tests and commands

Run Cargo commands from the Tauri directory so rustup sees the pinned toolchain file:

```bash
cd src/desktop/src-tauri
cargo test
cargo clippy --all-targets --all-features -- -D warnings
```

## Configuration

### .NET engine

The engine uses the .NET Generic Host configuration pipeline. Its content root is the built engine directory, so settings are loaded beside the DLL rather than from whichever directory launched Tauri.

The checked-in configuration layers are:

1. `src/engine/ChangeLens.Engine/appsettings.json` for safe defaults shared by every environment.
2. `src/engine/ChangeLens.Engine/appsettings.Development.json` for safe development overrides.
3. Environment variables for machine-local values and secrets, using `__` between nested keys.
4. Command-line settings when the engine is launched directly.

Set `DOTNET_ENVIRONMENT=Development` to load the development override. Both JSON files are copied into build and publish output. The current files only define logging levels because the implemented protocol slice has no database or AI settings yet.

Do not add secrets to either appsettings file. When AI providers are implemented, API keys belong in the engine process environment or secure credential storage, never in protocol messages or the React configuration.

Logging providers are deliberately disabled for now. This prevents Generic Host diagnostics from corrupting the standard-output protocol. A future structured logger must write to standard error or local files.

### React and Vite

Vite loads these checked-in, non-secret layers from `src/desktop/ui`:

1. `.env` for shared build-time values.
2. `.env.development` while the Vite development server is running.
3. `.env.local` and `.env.development.local` for ignored machine-local overrides.

Only variables prefixed with `VITE_` are exposed to browser code, and those values are embedded in the built JavaScript. Never put credentials, tokens, repository secrets, or unrestricted filesystem paths in a `VITE_` variable. The checked-in files are intentionally comment-only until the UI has a real configurable value.

### Tauri shell

Tauri's checked-in application settings live in `src/desktop/src-tauri/tauri.conf.json`. The development engine path normally needs no override: debug builds resolve `src/engine/ChangeLens.Engine/bin/Debug/net10.0/ChangeLens.Engine.dll`.

To test a different built engine, set `CHANGELENS_ENGINE_PATH` before starting Tauri.

macOS or Linux, from `src/desktop`:

```bash
CHANGELENS_ENGINE_PATH=/absolute/path/to/ChangeLens.Engine.dll npm run desktop:dev
```

Windows PowerShell, from `src/desktop`:

```powershell
$env:CHANGELENS_ENGINE_PATH = "C:\absolute\path\to\ChangeLens.Engine.dll"
npm run desktop:dev
```

Release builds require `CHANGELENS_ENGINE_PATH` until packaging adds the self-contained engine as an application resource.

## Ports and local resources

| Port or resource | Owner | When used |
| --- | --- | --- |
| `127.0.0.1:5173/tcp` | Vite | Browser and Tauri development server; the port is strict |
| `5174/tcp` | Vite HMR | Only when `TAURI_DEV_HOST` enables remote or device development |
| Standard input/output | Tauri ↔ .NET engine | Versioned newline-delimited JSON; not a network port |
| Local file, no port | SQLite | Planned embedded persistence |
| Host process, no port | Git CLI and repository SDKs | Repository facts and optional verification capabilities |

No Compose container exposes a port today. When a real external service is implemented, add it to `compose.yaml` with a fixed localhost-only binding, health check, and documented data lifecycle, then add its port to this table.

## Verification commands

Run the complete current check set before submitting changes.

Engine build and integration tests:

```bash
dotnet test src/engine/ChangeLens.slnx --configuration Debug
```

React checks:

```bash
cd src/desktop
npm run build
npm run typecheck
npm run lint
npm test
npm run format:check
```

Tauri checks:

```bash
cd src/desktop/src-tauri
cargo test
cargo fmt --check
cargo clippy --all-targets --all-features -- -D warnings
```

## Troubleshooting

### `Desktop engine unavailable`

- Confirm `dotnet --version` selects a .NET 10 SDK.
- Run `dotnet build src/engine/ChangeLens.Engine/ChangeLens.Engine.csproj --nologo` from the repository root.
- Confirm `src/engine/ChangeLens.Engine/bin/Debug/net10.0/ChangeLens.Engine.dll` exists.
- Remove an incorrect `CHANGELENS_ENGINE_PATH` override.
- Start with `npm run desktop:dev`, not browser-only `npm run dev`.

### Vite reports that port 5173 is already in use

The port is intentionally fixed because Tauri loads `http://localhost:5173`. Stop the process using that port and rerun the command; do not let Vite choose a different port.

### Rust or native-library build failures

Run Cargo from `src/desktop/src-tauri`, check `rustup show active-toolchain`, and confirm the platform-specific Tauri prerequisites are installed. On Linux, check the exact package list for the distribution rather than substituting WebKitGTK 4.0 for 4.1.

### Clean local build output

Delete only generated build artifacts, then restore them with the setup commands:

```bash
dotnet clean src/engine/ChangeLens.slnx
cd src/desktop/src-tauri
cargo clean
```

`node_modules`, .NET `bin`/`obj`, Rust `target`, local databases, logs, and `.env.local` files are ignored by Git. Do not delete `.changelens` once it contains development data unless losing that local state is intentional.

## Packaged-user dependencies

The eventual installed application will bundle the compiled frontend, Tauri shell, and a self-contained .NET engine. End users will not need Rust, Node.js, npm, the .NET runtime, or Docker. Git is the only expected host dependency. Repository-specific SDKs and test tools are optional capabilities that ChangeLens detects and reports when unavailable; remote AI additionally requires an explicitly configured provider, credentials, and network access.
