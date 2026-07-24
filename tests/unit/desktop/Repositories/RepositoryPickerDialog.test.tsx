import "@testing-library/jest-dom/vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { RepositoryPickerDialog } from "../../../../src/desktop/ui/src/Repositories/Components/RepositoryPickerDialog";
import { createResolvablePromise } from "../Support/createResolvablePromise";

afterEach(cleanup);

describe("RepositoryPickerDialog", () => {
  it("provides an accessible non-dismissible first-launch native dialog", () => {
    render(
      <RepositoryPickerDialog
        dismissible={false}
        onDismiss={vi.fn()}
        onOpenRepository={vi.fn()}
      />,
    );

    expect(
      screen.getByRole("dialog", { name: "Open a repository" }),
    ).toHaveAttribute("aria-modal", "true");
    expect(
      screen.getByText(
        "Choose a Git working tree for ChangeLens to inspect locally and read-only.",
      ),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Choose folder" })).toHaveFocus();
    expect(
      screen.queryByRole("button", { name: "Cancel" }),
    ).not.toBeInTheDocument();
  });

  it("announces opening progress and allows a replacement dialog to dismiss", async () => {
    const user = userEvent.setup();
    const onOpenRepository = vi.fn();
    const onDismiss = vi.fn();
    render(
      <RepositoryPickerDialog
        dismissible
        onDismiss={onDismiss}
        onOpenRepository={onOpenRepository}
        selectedPath="/repo"
      />,
    );

    await user.click(screen.getByRole("button", { name: "Cancel" }));
    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it("announces the selected path as polite text while opening", async () => {
    const user = userEvent.setup();
    const repositoryOpen = createResolvablePromise<{
      name: string;
      canonicalPath: string;
      head: { kind: "branch"; name: string; revision: string };
    }>();
    render(
      <RepositoryPickerDialog
        dismissible={false}
        onDismiss={vi.fn()}
        onOpenRepository={() => repositoryOpen.promise}
        selectFolder={async () => '<img onerror="alert(1)">'}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Choose folder" }));

    expect(screen.getByRole("status")).toHaveAttribute("aria-live", "polite");
    expect(screen.getByRole("status")).toHaveTextContent(
      'Inspecting repository… <img onerror="alert(1)">',
    );
    expect(screen.getByRole("status").querySelector("code")).toHaveTextContent(
      '<img onerror="alert(1)">',
    );
    expect(document.querySelector("img[onerror]")).not.toBeInTheDocument();
  });

  it("shows ordered known errors and lets the user choose another folder", async () => {
    const user = userEvent.setup();
    const selectFolder = vi
      .fn()
      .mockResolvedValueOnce("/not-a-repository")
      .mockResolvedValueOnce(null);
    render(
      <RepositoryPickerDialog
        dismissible={false}
        onDismiss={vi.fn()}
        onOpenRepository={async () => {
          throw {
            kind: "operation",
            errors: [
              {
                type: "Validation",
                code: "repository.notGitRepository",
                message: "The first message.",
              },
              {
                type: "ExternalDependencyFailure",
                code: "git.unavailable",
                message: "The second message.",
              },
            ],
          };
        }}
        selectFolder={selectFolder}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Choose folder" }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      "Not a Git working tree",
    );
    expect(
      screen.getAllByRole("listitem").map((item) => item.textContent),
    ).toEqual(["The first message.", "The second message."]);
    await user.click(screen.getByRole("button", { name: "Choose folder" }));
    expect(selectFolder).toHaveBeenCalledTimes(2);
  });

  it("dismisses a replacement picker when Escape is pressed", () => {
    const onDismiss = vi.fn();
    render(
      <RepositoryPickerDialog
        dismissible
        onDismiss={onDismiss}
        onOpenRepository={vi.fn()}
      />,
    );

    fireEvent(
      screen.getByRole("dialog"),
      new Event("cancel", { cancelable: true }),
    );

    expect(onDismiss).toHaveBeenCalledOnce();
  });

  it("uses a native open dialog and restores focus when it closes", () => {
    const launcher = document.createElement("button");
    document.body.append(launcher);
    launcher.focus();
    const { unmount } = render(
      <RepositoryPickerDialog
        dismissible
        onDismiss={vi.fn()}
        onOpenRepository={vi.fn()}
      />,
    );

    expect(screen.getByRole("dialog")).toHaveProperty("open", true);
    expect(screen.getByRole("button", { name: "Choose folder" })).toHaveFocus();
    unmount();

    expect(launcher).toHaveFocus();
    launcher.remove();
  });

  it("ignores a late folder selection after unmount", async () => {
    const user = userEvent.setup();
    const folderSelection = createResolvablePromise<string | null>();
    const onRepositoryOpened = vi.fn();
    const { unmount } = render(
      <RepositoryPickerDialog
        dismissible={false}
        onDismiss={vi.fn()}
        onOpenRepository={vi.fn()}
        onRepositoryOpened={onRepositoryOpened}
        selectFolder={() => folderSelection.promise}
      />,
    );

    await user.click(screen.getByRole("button", { name: "Choose folder" }));
    unmount();
    folderSelection.resolve("/late-repository");
    await Promise.resolve();
    await Promise.resolve();
    await Promise.resolve();

    expect(onRepositoryOpened).not.toHaveBeenCalled();
  });
});
