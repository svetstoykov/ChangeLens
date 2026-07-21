import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { EngineStatusClient } from "../Interfaces/EngineStatusClient";

export class TauriEngineStatusClient implements EngineStatusClient {
  async checkStatus(): Promise<void> {
    try {
      await invoke<void>("engine_check_status");
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
