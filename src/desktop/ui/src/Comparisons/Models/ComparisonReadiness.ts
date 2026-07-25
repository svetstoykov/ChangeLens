export type ComparisonReadiness =
  | { readonly state: "ready" }
  | { readonly state: "empty" }
  | { readonly state: "conflicts"; readonly conflictedFileCount: number };
