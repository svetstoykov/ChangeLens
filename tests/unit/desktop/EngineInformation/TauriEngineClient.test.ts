import { invoke } from "@tauri-apps/api/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TauriEngineClient } from "../../../../src/desktop/ui/src/EngineInformation/Services/TauriEngineClient";

vi.mock("@tauri-apps/api/core", () => ({
  invoke: vi.fn(),
}));

describe("TauriEngineClient", () => {
  beforeEach(() => {
    vi.mocked(invoke).mockReset();
  });

  it("gets engine information through the narrow Tauri command", async () => {
    const information = {
      name: "ChangeLens.Engine",
      version: "0.1.0",
      protocolVersion: 1,
    };
    vi.mocked(invoke).mockResolvedValue(information);
    const client = new TauriEngineClient();

    await expect(client.getInformation()).resolves.toEqual(information);
    expect(invoke).toHaveBeenCalledExactlyOnceWith("engine_get_info");
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
    const client = new TauriEngineClient();

    await expect(client.getInformation()).rejects.toMatchObject({
      name: "ActionError",
      kind: "operation",
      requestId: "desktop-43",
      errors: [
        { type: "Validation", code: "fixture.first" },
        { type: "Conflict", code: "fixture.second" },
      ],
    });
    expect(invoke).toHaveBeenCalledExactlyOnceWith("engine_get_info");
  });
});
