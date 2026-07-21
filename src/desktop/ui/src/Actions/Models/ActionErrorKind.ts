export const actionErrorKinds = [
  "operation",
  "transport",
  "protocol",
  "unexpected",
] as const;

export type ActionErrorKind = (typeof actionErrorKinds)[number];

export function isActionErrorKind(value: unknown): value is ActionErrorKind {
  return actionErrorKinds.some((kind) => kind === value);
}
