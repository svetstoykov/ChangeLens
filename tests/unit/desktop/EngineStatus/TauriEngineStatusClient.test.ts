import { invoke } from "@tauri-apps/api/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TauriEngineStatusClient } from "../../../../src/desktop/ui/src/EngineStatus/Services/TauriEngineStatusClient";

vi.mock("@tauri-apps/api/core", () => ({
  invoke: vi.fn(),
}));

describe("TauriEngineStatusClient", () => {
  beforeEach(() => {
    vi.mocked(invoke).mockReset();
  });

  it("checks engine readiness through the narrow Tauri command", async () => {
    vi.mocked(invoke).mockResolvedValue(undefined);
    const client = new TauriEngineStatusClient();

    await expect(client.checkStatus()).resolves.toBeUndefined();
    expect(invoke).toHaveBeenCalledExactlyOnceWith("engine_check_status");
  });

  it("normalizes an ordered Engine action rejection", async () => {
    vi.mocked(invoke).mockRejectedValue({
      kind: "operation",
      requestId: "desktop-43",
      errors: [
        {
          type: "Validation",
          code: "fixture.first",
          message: "The first fixture value is invalid.",
        },
        {
          type: "Conflict",
          code: "fixture.second",
          message: "The second fixture value conflicts with current state.",
        },
      ],
    });
    const client = new TauriEngineStatusClient();

    await expect(client.checkStatus()).rejects.toMatchObject({
      name: "ActionError",
      kind: "operation",
      requestId: "desktop-43",
      errors: [
        { type: "Validation", code: "fixture.first" },
        { type: "Conflict", code: "fixture.second" },
      ],
    });
    expect(invoke).toHaveBeenCalledExactlyOnceWith("engine_check_status");
  });

  it("normalizes an Engine rejection without a request identifier", async () => {
    vi.mocked(invoke).mockRejectedValue({
      kind: "operation",
      errors: [
        {
          type: "Validation",
          code: "protocol.invalidRequest",
          message: "The request does not match the engine protocol schema.",
        },
      ],
    });
    const client = new TauriEngineStatusClient();

    await expect(client.checkStatus()).rejects.toMatchObject({
      name: "ActionError",
      kind: "operation",
      requestId: undefined,
      errors: [{ type: "Validation", code: "protocol.invalidRequest" }],
    });
    expect(invoke).toHaveBeenCalledExactlyOnceWith("engine_check_status");
  });
});
