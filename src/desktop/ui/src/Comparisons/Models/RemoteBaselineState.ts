export type RemoteBaselineState =
  | { readonly state: "current"; readonly remoteRevision: string }
  | { readonly state: "moved"; readonly remoteRevision: string }
  | { readonly state: "noRemote" };
