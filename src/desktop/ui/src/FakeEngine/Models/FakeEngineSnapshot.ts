import type { ColorTheme } from "../../Visuals/Models/ColorTheme";
import type { FakeRun } from "./FakeRun";

/** The fake engine state kept in `sessionStorage` across page reloads. */
export interface FakeEngineSnapshot {
  readonly colorTheme: ColorTheme | null;
  readonly isInHistory: boolean;
  readonly lastOpenedAt: number;
  readonly nextRunNumber: number;
  readonly runs: readonly FakeRun[];
}
