import { invoke } from "@tauri-apps/api/core";
import { normalizeActionError } from "../../Actions/Services/normalizeActionError";
import type { RepositoryFolderPicker } from "../Interfaces/RepositoryFolderPicker";

export class TauriRepositoryFolderPicker implements RepositoryFolderPicker {
  async selectFolder(): Promise<string | null> {
    try {
      return await invoke<string | null>("select_repository_folder");
    } catch (error: unknown) {
      throw normalizeActionError(error);
    }
  }
}
