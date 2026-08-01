export interface AnalysisRepository {
  readonly repositoryId: string;
  readonly displayName: string;
  readonly canonicalPath: string;
  readonly head: string;
}
