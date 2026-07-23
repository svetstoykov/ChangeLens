export interface BranchRepositoryHead {
  readonly kind: "branch";
  readonly name: string;
  readonly revision: string;
}
