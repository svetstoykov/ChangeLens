import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { RepositoryClient } from "../Interfaces/RepositoryClient";
import type { OpenedRepository } from "../Models/OpenedRepository";

export class TauriRepositoryClient implements RepositoryClient {
  async openRepository(path: string): Promise<OpenedRepository> {
    try {
      return await invoke<OpenedRepository>("repository_open", { path });
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
