import { ActionError } from "../Models/ActionError";
import type {
  ActionErrorDetail,
  ActionErrorDetails,
} from "../Models/ActionErrorDetail";
import { isActionErrorKind } from "../Models/ActionErrorKind";
import { isOperationErrorType } from "../Models/OperationErrorType";

const unexpectedErrors: ActionErrorDetails = [
  {
    type: "InternalError",
    code: "desktop.unexpectedFailure",
    message: "The desktop action failed unexpectedly.",
  },
];

export function normalizeActionError(value: unknown): ActionError {
  if (value instanceof ActionError) {
    return value;
  }

  if (!isRecord(value) || !isActionErrorKind(value.kind)) {
    return unexpectedActionError();
  }

  const requestId = value.requestId;
  if (
    requestId !== undefined &&
    (typeof requestId !== "string" || requestId.trim().length === 0)
  ) {
    return unexpectedActionError();
  }

  if (!Array.isArray(value.errors) || value.errors.length === 0) {
    return unexpectedActionError();
  }

  const errors: ActionErrorDetail[] = [];
  for (const valueError of value.errors) {
    const error = toActionErrorDetail(valueError);
    if (error === undefined) {
      return unexpectedActionError();
    }

    errors.push(error);
  }

  const [firstError, ...remainingErrors] = errors;
  if (firstError === undefined) {
    return unexpectedActionError();
  }

  return new ActionError(
    value.kind,
    [firstError, ...remainingErrors],
    requestId,
  );
}

function toActionErrorDetail(value: unknown): ActionErrorDetail | undefined {
  if (
    !isRecord(value) ||
    !isOperationErrorType(value.type) ||
    typeof value.code !== "string" ||
    value.code.trim().length === 0 ||
    typeof value.message !== "string" ||
    value.message.trim().length === 0
  ) {
    return undefined;
  }

  return {
    type: value.type,
    code: value.code,
    message: value.message,
  };
}

function unexpectedActionError(): ActionError {
  return new ActionError("unexpected", unexpectedErrors);
}

function isRecord(value: unknown): value is Record<string, unknown> {
  return typeof value === "object" && value !== null && !Array.isArray(value);
}
