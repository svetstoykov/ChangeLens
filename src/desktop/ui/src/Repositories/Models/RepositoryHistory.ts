import type { RecentRepository } from "./RecentRepository";

export interface RepositoryHistory {
  readonly lastRepositoryId: string | null;
  readonly repositories: readonly RecentRepository[];
}
