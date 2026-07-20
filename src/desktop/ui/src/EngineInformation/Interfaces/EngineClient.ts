import type { EngineInformation } from "../Models/EngineInformation";

export interface EngineClient {
  getInformation(): Promise<EngineInformation>;
}
