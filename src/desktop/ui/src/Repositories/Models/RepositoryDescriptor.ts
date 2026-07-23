import type { RepositoryHead } from "./RepositoryHead";

export interface RepositoryDescriptor {
  readonly name: string;
  readonly canonicalPath: string;
  readonly head: RepositoryHead;
}
