import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";
import { ActionError } from "../../../../src/desktop/ui/src/Actions/Models/ActionError";
import type { ActionErrorKind } from "../../../../src/desktop/ui/src/Actions/Models/ActionErrorKind";
import { normalizeActionError } from "../../../../src/desktop/ui/src/Actions/Services/normalizeActionError";

describe("normalizeActionError", () => {
  it("preserves the shared ordered Engine error fixture", () => {
    const fixture = JSON.parse(
      readFileSync(
        resolve(
          process.cwd(),
          "../../../contracts/engine-protocol/v1/fixtures/ordered-errors.response.json",
        ),
        "utf8",
      ),
    );
    const rejection = {
      kind: "operation",
      requestId: fixture.requestId,
      errors: fixture.errors,
    };

    const error = normalizeActionError(rejection);

    expect(error).toBeInstanceOf(ActionError);
    expect(error.kind).toBe("operation");
    expect(error.requestId).toBe("desktop-43");
    expect(error.errors.map((detail) => detail.code)).toEqual([
      "fixture.first",
      "fixture.second",
    ]);
    expect(error.message).toBe("The first fixture value is invalid.");
  });

  it.each<ActionErrorKind>([
    "operation",
    "transport",
    "protocol",
    "unexpected",
  ])("preserves a valid %s rejection", (kind) => {
    const error = normalizeActionError({
      kind,
      requestId: "desktop-1",
      errors: [
        {
          type: "InternalError",
          code: "fixture.code",
          message: "Safe fixture message.",
        },
      ],
    });

    expect(error.kind).toBe(kind);
    expect(error.requestId).toBe("desktop-1");
    expect(error.errors[0].code).toBe("fixture.code");
  });

  it("preserves a valid rejection without a request identifier", () => {
    const error = normalizeActionError({
      kind: "operation",
      errors: [
        {
          type: "Validation",
          code: "protocol.invalidRequest",
          message: "The request does not match the engine protocol schema.",
        },
      ],
    });

    expect(error.kind).toBe("operation");
    expect(error.requestId).toBeUndefined();
    expect(error.errors).toEqual([
      {
        type: "Validation",
        code: "protocol.invalidRequest",
        message: "The request does not match the engine protocol schema.",
      },
    ]);
  });

  it.each([
    null,
    new Error("sensitive raw error"),
    { kind: "unknown", errors: [] },
    { kind: "operation", errors: [] },
    {
      kind: "operation",
      errors: [{ type: "Unknown", code: "fixture", message: "bad" }],
    },
  ])("sanitizes malformed rejection %#", (rejection) => {
    const error = normalizeActionError(rejection);

    expect(error.kind).toBe("unexpected");
    expect(error.errors).toEqual([
      {
        type: "InternalError",
        code: "desktop.unexpectedFailure",
        message: "The desktop action failed unexpectedly.",
      },
    ]);
    expect(error.message).not.toContain("sensitive");
  });

  it("returns an existing ActionError unchanged", () => {
    const existing = normalizeActionError({
      kind: "transport",
      errors: [
        {
          type: "Timeout",
          code: "engine.responseTimedOut",
          message: "The engine timed out.",
        },
      ],
    });

    expect(normalizeActionError(existing)).toBe(existing);
  });
});
