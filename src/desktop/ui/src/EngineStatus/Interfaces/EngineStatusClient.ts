export interface EngineStatusClient {
  checkStatus(): Promise<void>;
}
