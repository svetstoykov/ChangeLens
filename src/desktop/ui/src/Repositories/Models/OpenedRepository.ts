import type { RepositoryDescriptor } from "./RepositoryDescriptor";

export interface OpenedRepository {
  readonly repositoryId: string;
  readonly repository: RepositoryDescriptor;
  readonly preferredTarget: string | null;
}
