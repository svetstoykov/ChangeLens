import type { RepositoryDescriptor } from "../Models/RepositoryDescriptor";

export interface RepositoryClient {
  openRepository(path: string): Promise<RepositoryDescriptor>;
}
