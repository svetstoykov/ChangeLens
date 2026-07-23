import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { RepositoryClient } from "../Interfaces/RepositoryClient";
import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";

export class TauriRepositoryClient implements RepositoryClient {
  async openRepository(path: string): Promise<RepositoryDescriptor> {
    try {
      return await invoke<RepositoryDescriptor>("repository_open", { path });
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
