const targetRefPrefixes = ["refs/heads/", "refs/remotes/"] as const;

/** Returns the branch name shown in setup, such as `main` or `origin/main`, for a full Git ref. */
export function shortenTargetRef(fullName: string): string {
  const prefix = targetRefPrefixes.find((item) => fullName.startsWith(item));
  return prefix === undefined ? fullName : fullName.slice(prefix.length);
}
