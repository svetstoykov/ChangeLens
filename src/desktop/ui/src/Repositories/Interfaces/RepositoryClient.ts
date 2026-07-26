import type { OpenedRepository } from "../Models/OpenedRepository";

export interface RepositoryClient {
  openRepository(path: string): Promise<OpenedRepository>;
}
