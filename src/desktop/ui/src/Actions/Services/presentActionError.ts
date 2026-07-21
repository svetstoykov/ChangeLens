import type { ActionError } from "../Models/ActionError";
import type { ActionErrorPresentation } from "../Models/ActionErrorPresentation";
import type { OperationErrorType } from "../Models/OperationErrorType";

const titlesByType: Readonly<Record<OperationErrorType, string>> = {
  NotFound: "Requested item not found",
  Validation: "Check the supplied values",
  MalformedInput: "Input could not be read",
  UnprocessableInput: "Input is not supported",
  Conflict: "Action conflicts with current state",
  InvalidOperation: "Action is unavailable",
  Unauthorized: "Access is required",
  Timeout: "Action timed out",
  ExternalDependencyFailure: "Engine dependency unavailable",
  InternalError: "Unexpected failure",
};

export function presentActionError(
  error: ActionError,
  titlesByCode: Readonly<Record<string, string>> = {},
): ActionErrorPresentation {
  const firstError = error.errors[0];

  return {
    title: titlesByCode[firstError.code] ?? titlesByType[firstError.type],
    messages: error.errors.map((detail) => detail.message),
    requestId: error.requestId,
  };
}
