import type { RepositoryDescriptor } from "./RepositoryDescriptor";

export type RepositoryRestoreResult =
  | { readonly state: "none" }
  | {
      readonly state: "restored";
      readonly repositoryId: string;
      readonly repository: RepositoryDescriptor;
      readonly preferredTarget: string | null;
    };
