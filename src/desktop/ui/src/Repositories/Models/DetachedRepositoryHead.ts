export interface DetachedRepositoryHead {
  readonly kind: "detached";
  readonly revision: string;
}
