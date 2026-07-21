import { describe, expect, it } from "vitest";
import { ActionError } from "../../../../src/desktop/ui/src/Actions/Models/ActionError";
import { operationErrorTypes } from "../../../../src/desktop/ui/src/Actions/Models/OperationErrorType";
import { presentActionError } from "../../../../src/desktop/ui/src/Actions/Services/presentActionError";

describe("presentActionError", () => {
  it("uses a recognized stable code before the broad type", () => {
    const error = new ActionError(
      "transport",
      [
        {
          type: "Timeout",
          code: "engine.responseTimedOut",
          message: "The engine did not answer in time.",
        },
      ],
      "desktop-9",
    );

    const presentation = presentActionError(error, {
      "engine.responseTimedOut": "Engine response timed out",
    });

    expect(presentation.title).toBe("Engine response timed out");
    expect(presentation.messages).toEqual([
      "The engine did not answer in time.",
    ]);
    expect(presentation.requestId).toBe("desktop-9");
  });

  it.each(operationErrorTypes)("presents the %s error type", (type) => {
    const error = new ActionError("operation", [
      { type, code: `fixture.${type}`, message: `${type} message.` },
    ]);

    const presentation = presentActionError(error);

    expect(presentation.title).not.toHaveLength(0);
    expect(presentation.messages).toEqual([`${type} message.`]);
  });

  it("preserves every message in error order", () => {
    const error = new ActionError("operation", [
      { type: "Validation", code: "first", message: "First." },
      { type: "Conflict", code: "second", message: "Second." },
    ]);

    expect(presentActionError(error).messages).toEqual(["First.", "Second."]);
  });
});
