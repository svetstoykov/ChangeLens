export const operationErrorTypes = [
  "NotFound",
  "Validation",
  "MalformedInput",
  "UnprocessableInput",
  "Conflict",
  "InvalidOperation",
  "Unauthorized",
  "Timeout",
  "ExternalDependencyFailure",
  "InternalError",
] as const;

export type OperationErrorType = (typeof operationErrorTypes)[number];

export function isOperationErrorType(
  value: unknown,
): value is OperationErrorType {
  return operationErrorTypes.some((type) => type === value);
}
