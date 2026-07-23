import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { invoke } from "@tauri-apps/api/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import { TauriRepositoryFolderPicker } from "../../../../src/desktop/ui/src/Repositories/Services/TauriRepositoryFolderPicker";

vi.mock("@tauri-apps/api/core", () => ({
  invoke: vi.fn(),
}));

describe("TauriRepositoryFolderPicker", () => {
  beforeEach(() => {
    vi.mocked(invoke).mockReset();
  });

  it("returns the selected Unicode folder from the explicit Tauri command", async () => {
    vi.mocked(invoke).mockResolvedValue("/tmp/Проекти/change_lens");
    const picker = new TauriRepositoryFolderPicker();

    await expect(picker.selectFolder()).resolves.toBe(
      "/tmp/Проекти/change_lens",
    );
    expect(invoke).toHaveBeenCalledExactlyOnceWith("select_repository_folder");
  });

  it("returns null when folder selection is cancelled", async () => {
    vi.mocked(invoke).mockResolvedValue(null);
    const picker = new TauriRepositoryFolderPicker();

    await expect(picker.selectFolder()).resolves.toBeNull();
    expect(invoke).toHaveBeenCalledExactlyOnceWith("select_repository_folder");
  });

  it.each([
    {
      code: "desktop.folderPickerUnavailable",
      type: "ExternalDependencyFailure",
      message: "The desktop folder picker is unavailable.",
    },
    {
      code: "repository.pathEncodingUnsupported",
      type: "UnprocessableInput",
      message: "The selected path cannot be represented as Unicode.",
    },
  ])("normalizes the native $code rejection", async (detail) => {
    vi.mocked(invoke).mockRejectedValue({
      kind: "transport",
      errors: [detail],
    });
    const picker = new TauriRepositoryFolderPicker();

    await expect(picker.selectFolder()).rejects.toMatchObject({
      name: "ActionError",
      kind: "transport",
      requestId: undefined,
      errors: [detail],
    });
    expect(invoke).toHaveBeenCalledExactlyOnceWith("select_repository_folder");
  });

  it("sanitizes a malformed folder-picker rejection", async () => {
    vi.mocked(invoke).mockRejectedValue(
      new Error("sensitive native picker rejection"),
    );
    const picker = new TauriRepositoryFolderPicker();

    await expect(picker.selectFolder()).rejects.toMatchObject({
      name: "ActionError",
      kind: "unexpected",
      errors: [
        {
          type: "InternalError",
          code: "desktop.unexpectedFailure",
          message: "The desktop action failed unexpectedly.",
        },
      ],
    });
  });

  it("does not expose Engine protocol metadata or action names", () => {
    const source = readFileSync(
      resolve(
        process.cwd(),
        "src/Repositories/Services/TauriRepositoryFolderPicker.ts",
      ),
      "utf8",
    );

    expect(source).not.toContain("protocolVersion");
    expect(source).not.toContain("requestId");
    expect(source).not.toContain("repositories.open");
  });
});
