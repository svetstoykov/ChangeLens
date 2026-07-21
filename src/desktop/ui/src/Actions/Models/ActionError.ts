import type { ActionErrorDetails } from "./ActionErrorDetail";
import type { ActionErrorKind } from "./ActionErrorKind";

export class ActionError extends Error {
  readonly kind: ActionErrorKind;
  readonly requestId?: string;
  readonly errors: ActionErrorDetails;

  constructor(
    kind: ActionErrorKind,
    errors: ActionErrorDetails,
    requestId?: string,
  ) {
    super(errors[0].message);
    this.name = "ActionError";
    this.kind = kind;
    this.requestId = requestId;
    this.errors = Object.freeze([...errors]) as unknown as ActionErrorDetails;
  }
}
