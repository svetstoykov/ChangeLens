import type { RepositoryDescriptor } from "../../Repositories/Models/RepositoryDescriptor";
import type { ComparisonReadiness } from "./ComparisonReadiness";
import type { ComparisonTarget } from "./ComparisonTarget";

export interface PreparedComparison {
  readonly repository: RepositoryDescriptor;
  readonly target: ComparisonTarget;
  readonly mergeBaseRevision: string;
  readonly currentWorkCommitCount: number;
  readonly targetOnlyCommitCount: number;
  readonly changedFileTotal: number;
  readonly uncommittedFileTotal: number;
  readonly stagedFileCount: number;
  readonly unstagedFileCount: number;
  readonly untrackedFileCount: number;
  readonly readiness: ComparisonReadiness;
  readonly freshnessToken: string;
}
