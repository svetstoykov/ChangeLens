import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "../../../../src/desktop/ui/src/App";
import type { EngineStatusClient } from "../../../../src/desktop/ui/src/EngineStatus/Interfaces/EngineStatusClient";
import type { RepositoryClient } from "../../../../src/desktop/ui/src/Repositories/Interfaces/RepositoryClient";
import type { RepositoryFolderPicker } from "../../../../src/desktop/ui/src/Repositories/Interfaces/RepositoryFolderPicker";
import { createResolvablePromise } from "../Support/createResolvablePromise";

afterEach(cleanup);

const readyEngine: EngineStatusClient = { checkStatus: async () => undefined };

describe("repository intake journey", () => {
  it("opens the first-launch picker after the engine is ready and opens the selected repository", async () => {
    const user = userEvent.setup();
    const repositoryFolderPicker: RepositoryFolderPicker = {
      selectFolder: vi.fn().mockResolvedValue("/repo"),
    };
    const repositoryClient: RepositoryClient = {
      openRepository: vi.fn().mockResolvedValue({
        name: "ChangeLens",
        canonicalPath: "/repo",
        head: { kind: "branch", name: "main", revision: "abc123" },
      }),
    };

    renderApp(readyEngine, repositoryClient, repositoryFolderPicker);
    await user.click(
      await screen.findByRole("button", { name: "Choose folder" }),
    );

    expect(repositoryFolderPicker.selectFolder).toHaveBeenCalledOnce();
    expect(repositoryClient.openRepository).toHaveBeenCalledWith("/repo");
    expect(
      await screen.findByRole("heading", { name: "ChangeLens", level: 2 }),
    ).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("keeps the current repository when replacement is dismissed", async () => {
    const user = userEvent.setup();
    const repositoryFolderPicker: RepositoryFolderPicker = {
      selectFolder: vi
        .fn()
        .mockResolvedValueOnce("/current")
        .mockResolvedValueOnce(null),
    };
    const repositoryClient: RepositoryClient = {
      openRepository: vi.fn().mockResolvedValue({
        name: "Current repository",
        canonicalPath: "/current",
        head: { kind: "detached", revision: "abc123" },
      }),
    };

    renderApp(readyEngine, repositoryClient, repositoryFolderPicker);
    await user.click(
      await screen.findByRole("button", { name: "Choose folder" }),
    );
    await screen.findByText("Current repository");
    await user.click(
      screen.getByRole("button", { name: "Open another repository" }),
    );
    await user.click(screen.getByRole("button", { name: "Cancel" }));

    expect(screen.getByText("Current repository")).toBeInTheDocument();
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
    expect(repositoryClient.openRepository).toHaveBeenCalledOnce();
  });

  it("keeps first launch open when folder selection is cancelled", async () => {
    const user = userEvent.setup();
    const repositoryFolderPicker: RepositoryFolderPicker = {
      selectFolder: vi.fn().mockResolvedValue(null),
    };
    const repositoryClient: RepositoryClient = { openRepository: vi.fn() };

    renderApp(readyEngine, repositoryClient, repositoryFolderPicker);
    await user.click(
      await screen.findByRole("button", { name: "Choose folder" }),
    );

    expect(screen.getByRole("dialog")).toBeInTheDocument();
    expect(repositoryClient.openRepository).not.toHaveBeenCalled();
  });

  it("preserves a repository through a failed replacement and commits a later success", async () => {
    const user = userEvent.setup();
    const repositoryFolderPicker: RepositoryFolderPicker = {
      selectFolder: vi
        .fn()
        .mockResolvedValueOnce("/current")
        .mockResolvedValueOnce("/broken")
        .mockResolvedValueOnce("/next"),
    };
    const repositoryClient: RepositoryClient = {
      openRepository: vi
        .fn()
        .mockResolvedValueOnce(branchRepository("Current", "/current", "main"))
        .mockRejectedValueOnce({
          kind: "operation",
          errors: [
            {
              type: "Validation",
              code: "repository.notGitRepository",
              message: "Not a repository.",
            },
          ],
        })
        .mockResolvedValueOnce(branchRepository("Next", "/next", "release")),
    };

    renderApp(readyEngine, repositoryClient, repositoryFolderPicker);
    await user.click(
      await screen.findByRole("button", { name: "Choose folder" }),
    );
    await screen.findByRole("heading", { name: "Current", level: 2 });
    expect(screen.getByLabelText("Open repository")).toHaveAttribute(
      "data-repository-generation",
      "1",
    );
    await user.click(
      screen.getByRole("button", { name: "Open another repository" }),
    );
    await user.click(screen.getByRole("button", { name: "Choose folder" }));
    await screen.findByRole("alert");

    expect(
      screen.getByRole("heading", { name: "Current", level: 2 }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Open repository")).toHaveAttribute(
      "data-repository-generation",
      "1",
    );
    await user.click(screen.getByRole("button", { name: "Choose folder" }));

    expect(
      await screen.findByRole("heading", { name: "Next", level: 2 }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Open repository")).toHaveAttribute(
      "data-repository-generation",
      "2",
    );
    expect(screen.queryByRole("dialog")).not.toBeInTheDocument();
  });

  it("ignores a late replacement after Escape preserves the current repository", async () => {
    const user = userEvent.setup();
    const nextRepository =
      createResolvablePromise<ReturnType<typeof branchRepository>>();
    const repositoryFolderPicker: RepositoryFolderPicker = {
      selectFolder: vi
        .fn()
        .mockResolvedValueOnce("/current")
        .mockResolvedValueOnce("/next"),
    };
    const repositoryClient: RepositoryClient = {
      openRepository: vi
        .fn()
        .mockResolvedValueOnce(branchRepository("Current", "/current", "main"))
        .mockImplementationOnce(() => nextRepository.promise),
    };

    renderApp(readyEngine, repositoryClient, repositoryFolderPicker);
    await user.click(
      await screen.findByRole("button", { name: "Choose folder" }),
    );
    await screen.findByRole("heading", { name: "Current", level: 2 });
    await user.click(
      screen.getByRole("button", { name: "Open another repository" }),
    );
    await user.click(screen.getByRole("button", { name: "Choose folder" }));
    fireEvent(
      screen.getByRole("dialog"),
      new Event("cancel", { cancelable: true }),
    );
    nextRepository.resolve(branchRepository("Next", "/next", "release"));
    await Promise.resolve();
    await Promise.resolve();

    expect(
      screen.getByRole("heading", { name: "Current", level: 2 }),
    ).toBeInTheDocument();
    expect(
      screen.queryByRole("heading", { name: "Next", level: 2 }),
    ).not.toBeInTheDocument();
  });

  it("never renders unavailable product areas", async () => {
    renderApp(
      readyEngine,
      { openRepository: vi.fn() },
      { selectFolder: vi.fn().mockResolvedValue(null) },
    );
    await screen.findByRole("dialog");

    expect(
      screen.queryByText(
        /analyze|comparison|history|terminal|analytics|sync|recent analysis|mock repository result/i,
      ),
    ).not.toBeInTheDocument();
  });
});

function branchRepository(name: string, canonicalPath: string, branch: string) {
  return {
    name,
    canonicalPath,
    head: { kind: "branch" as const, name: branch, revision: "abc123" },
  };
}

function renderApp(
  engineStatusClient: EngineStatusClient,
  repositoryClient: RepositoryClient,
  repositoryFolderPicker: RepositoryFolderPicker,
) {
  return render(
    <App
      engineStatusClient={engineStatusClient}
      repositoryClient={repositoryClient}
      repositoryFolderPicker={repositoryFolderPicker}
    />,
  );
}
