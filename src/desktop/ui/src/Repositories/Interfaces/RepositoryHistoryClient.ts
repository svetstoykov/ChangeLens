import type { RepositoryHistory } from "../Models/RepositoryHistory";
import type { RepositoryRestoreResult } from "../Models/RepositoryRestoreResult";

export interface RepositoryHistoryClient {
  restoreLastRepository(): Promise<RepositoryRestoreResult>;
  listRecentRepositories(): Promise<RepositoryHistory>;
  removeRecentRepository(repositoryId: string): Promise<void>;
}
