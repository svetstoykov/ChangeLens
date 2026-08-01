import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { AnalysisClient } from "../Interfaces/AnalysisClient";
import type { AnalysisGetActiveResult } from "../Models/AnalysisGetActiveResult";
import type { AnalysisRunProjection } from "../Models/AnalysisRunProjection";
import type { AnalysisStartResult } from "../Models/AnalysisStartResult";

export class TauriAnalysisClient implements AnalysisClient {
  start(
    path: string,
    target: string,
    freshnessToken: string,
    changeContext: string | null,
  ): Promise<AnalysisStartResult> {
    return invoke<AnalysisStartResult>("analysis_start", {
      path,
      target,
      freshnessToken,
      changeContext,
    }).catch((error: unknown) => {
      throw normalizeActionError(error);
    });
  }

  getActive(path: string): Promise<AnalysisGetActiveResult> {
    return invoke<AnalysisGetActiveResult>("analysis_get_active", {
      path,
    }).catch((error: unknown) => {
      throw normalizeActionError(error);
    });
  }

  pollRun(runId: string): Promise<AnalysisRunProjection> {
    return invoke<AnalysisRunProjection>("analysis_poll_run", { runId }).catch(
      (error: unknown) => {
        throw normalizeActionError(error);
      },
    );
  }

  cancel(runId: string): Promise<void> {
    return invoke<void>("analysis_cancel", { runId }).catch(
      (error: unknown) => {
        throw normalizeActionError(error);
      },
    );
  }
}
