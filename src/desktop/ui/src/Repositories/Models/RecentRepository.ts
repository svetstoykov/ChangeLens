export interface RecentRepository {
  readonly repositoryId: string;
  readonly name: string;
  readonly canonicalPath: string;
  readonly lastOpenedAtUnixMilliseconds: number;
  readonly preferredTarget: string | null;
}
