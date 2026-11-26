import { mockIPC } from "@tauri-apps/api/mocks";
import { FakeEngine } from "./FakeEngine";

/**
 * Routes every Tauri `invoke` call to an in-memory fake engine. The
 * `fakeFail` query parameter takes a comma-separated list of command names
 * that reject, for example `?fakeFail=analysis_start,repository_open`.
 */
export function installFakeEngine(): void {
  const failingCommands = new Set(
    (new URLSearchParams(window.location.search).get("fakeFail") ?? "")
      .split(",")
      .map((command) => command.trim())
      .filter((command) => command.length > 0),
  );
  const engine = new FakeEngine(failingCommands);

  mockIPC((command, args) => engine.handle(command, args));
}
