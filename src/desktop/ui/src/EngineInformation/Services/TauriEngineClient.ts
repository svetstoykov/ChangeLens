import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { EngineClient } from "../Interfaces/EngineClient";
import type { EngineInformation } from "../Models/EngineInformation";

export class TauriEngineClient implements EngineClient {
  getInformation(): Promise<EngineInformation> {
    return this.invokeAction<EngineInformation>("engine_get_info");
  }

  private async invokeAction<T>(command: string): Promise<T> {
    try {
      return await invoke<T>(command);
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
