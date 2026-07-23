import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { invoke } from "@tauri-apps/api/core";
import { beforeEach, describe, expect, it, vi } from "vitest";
import type { RepositoryDescriptor } from "../../../../src/desktop/ui/src/Repositories/Models/RepositoryDescriptor";
import { TauriRepositoryClient } from "../../../../src/desktop/ui/src/Repositories/Services/TauriRepositoryClient";

vi.mock("@tauri-apps/api/core", () => ({
  invoke: vi.fn(),
}));

const branchRepository: RepositoryDescriptor = {
  name: "change_lens",
  canonicalPath: "/projects/change_lens",
  head: {
    kind: "branch",
    name: "main",
    revision: "0123456789abcdef0123456789abcdef01234567",
  },
};

const detachedRepository: RepositoryDescriptor = {
  name: "change_lens",
  canonicalPath: "/projects/change_lens",
  head: {
    kind: "detached",
    revision: "0123456789abcdef0123456789abcdef01234567",
  },
};

describe("TauriRepositoryClient", () => {
  beforeEach(() => {
    vi.mocked(invoke).mockReset();
  });

  it("opens a branch repository through the explicit Tauri command", async () => {
    vi.mocked(invoke).mockResolvedValue(branchRepository);
    const client = new TauriRepositoryClient();

    await expect(
      client.openRepository("/projects/change_lens"),
    ).resolves.toEqual(branchRepository);
    expect(invoke).toHaveBeenCalledExactlyOnceWith("repository_open", {
      path: "/projects/change_lens",
    });
  });

  it("returns a detached repository head without a branch name", async () => {
    vi.mocked(invoke).mockResolvedValue(detachedRepository);
    const client = new TauriRepositoryClient();

    await expect(
      client.openRepository("/projects/change_lens"),
    ).resolves.toEqual(detachedRepository);
  });

  it("preserves ordered Engine errors as an ActionError", async () => {
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
    const client = new TauriRepositoryClient();

    await expect(
      client.openRepository("/projects/change_lens"),
    ).rejects.toMatchObject({
      name: "ActionError",
      kind: "operation",
      requestId: "desktop-43",
      errors: [
        { type: "Validation", code: "fixture.first" },
        { type: "Conflict", code: "fixture.second" },
      ],
    });
  });

  it("sanitizes a malformed repository rejection", async () => {
    vi.mocked(invoke).mockRejectedValue(
      new Error("sensitive repository rejection"),
    );
    const client = new TauriRepositoryClient();

    await expect(
      client.openRepository("/projects/change_lens"),
    ).rejects.toMatchObject({
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
        "src/Repositories/Services/TauriRepositoryClient.ts",
      ),
      "utf8",
    );

    expect(source).not.toContain("protocolVersion");
    expect(source).not.toContain("requestId");
    expect(source).not.toContain("repositories.open");
  });

  it("composes and injects both repository boundaries at the React root", () => {
    const mainSource = readFileSync(
      resolve(process.cwd(), "src/main.tsx"),
      "utf8",
    );
    const appSource = readFileSync(
      resolve(process.cwd(), "src/App.tsx"),
      "utf8",
    );

    expect(mainSource).toContain(
      "const repositoryClient = new TauriRepositoryClient();",
    );
    expect(mainSource).toContain(
      "const repositoryFolderPicker = new TauriRepositoryFolderPicker();",
    );
    expect(mainSource).toContain("repositoryClient={repositoryClient}");
    expect(mainSource).toContain(
      "repositoryFolderPicker={repositoryFolderPicker}",
    );
    expect(appSource).toContain("repositoryClient: RepositoryClient;");
    expect(appSource).toContain(
      "repositoryFolderPicker: RepositoryFolderPicker;",
    );
  });
});
