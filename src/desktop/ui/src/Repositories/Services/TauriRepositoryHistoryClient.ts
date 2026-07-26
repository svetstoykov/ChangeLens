import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { RepositoryHistoryClient } from "../Interfaces/RepositoryHistoryClient";
import type { RepositoryHistory } from "../Models/RepositoryHistory";
import type { RepositoryRestoreResult } from "../Models/RepositoryRestoreResult";

export class TauriRepositoryHistoryClient implements RepositoryHistoryClient {
  async restoreLastRepository(): Promise<RepositoryRestoreResult> {
    try {
      return await invoke<RepositoryRestoreResult>("repository_restore_last");
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }

  async listRecentRepositories(): Promise<RepositoryHistory> {
    try {
      return await invoke<RepositoryHistory>("repository_list_recent");
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }

  async removeRecentRepository(repositoryId: string): Promise<void> {
    try {
      await invoke("repository_remove_recent", { repositoryId });
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
