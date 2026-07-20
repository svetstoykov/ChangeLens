import { invoke } from "@tauri-apps/api/core";
import type { EngineClient } from "../Interfaces/EngineClient";
import type { EngineInformation } from "../Models/EngineInformation";

export class TauriEngineClient implements EngineClient {
  getInformation(): Promise<EngineInformation> {
    return invoke<EngineInformation>("engine_get_info");
  }
}
