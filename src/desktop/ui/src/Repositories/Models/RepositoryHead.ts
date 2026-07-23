import type { BranchRepositoryHead } from "./BranchRepositoryHead";
import type { DetachedRepositoryHead } from "./DetachedRepositoryHead";

export type RepositoryHead = BranchRepositoryHead | DetachedRepositoryHead;
