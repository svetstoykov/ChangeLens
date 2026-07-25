import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { afterEach, describe, expect, it, vi } from "vitest";
import { App } from "../../../../src/desktop/ui/src/App";
import type { EngineStatusClient } from "../../../../src/desktop/ui/src/EngineStatus/Interfaces/EngineStatusClient";
import type { RepositoryClient } from "../../../../src/desktop/ui/src/Repositories/Interfaces/RepositoryClient";
import type { RepositoryFolderPicker } from "../../../../src/desktop/ui/src/Repositories/Interfaces/RepositoryFolderPicker";
import { createResolvablePromise } from "../Support/createResolvablePromise";

afterEach(cleanup);

const repositoryClient: RepositoryClient = {
  openRepository: async (path) => ({
    name: "change_lens",
    canonicalPath: path,
    head: {
      kind: "branch",
      name: "main",
      revision: "0123456789abcdef0123456789abcdef01234567",
    },
  }),
};

const repositoryFolderPicker: RepositoryFolderPicker = {
  selectFolder: async () => null,
};

describe("engine status", () => {
  it("shows a connection state while the engine request is pending", () => {
    const pendingStatus = createResolvablePromise<never>();
    const engineStatusClient: EngineStatusClient = {
      checkStatus: () => pendingStatus.promise,
    };

    renderApp(engineStatusClient);

    expect(
      screen.getByText("Connecting to the ChangeLens engine…"),
    ).toBeInTheDocument();
  });

  it("opens repository selection when the engine is ready", async () => {
    const engineStatusClient: EngineStatusClient = {
      checkStatus: async () => undefined,
    };

    renderApp(engineStatusClient);

    expect(
      await screen.findByRole("dialog", { name: "Open a repository" }),
    ).toBeInTheDocument();
  });

  it("uses the safe Engine-unavailable title for startup failures", async () => {
    renderApp({
      checkStatus: () =>
        Promise.reject({
          kind: "operation",
          errors: [
            {
              type: "ExternalDependencyFailure",
              code: "engine.startFailed",
              message: "The Engine process could not start.",
            },
          ],
        }),
    });

    expect(
      await screen.findByText("ChangeLens Engine unavailable"),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  it("uses the safe title for an unsupported protocol version", async () => {
    renderApp({
      checkStatus: () =>
        Promise.reject({
          kind: "operation",
          errors: [
            {
              type: "UnprocessableInput",
              code: "protocol.unsupportedVersion",
              message: "Protocol versions differ.",
            },
          ],
        }),
    });

    expect(
      await screen.findByText("Desktop and Engine do not match"),
    ).toBeInTheDocument();
  });

  it("presents malformed and unknown readiness failures safely with Retry", async () => {
    const user = userEvent.setup();
    const checkStatus = vi
      .fn()
      .mockRejectedValueOnce({
        kind: "operation",
        errors: [
          {
            type: "MalformedInput",
            code: "protocol.invalidResponse",
            message: "The response could not be read.",
          },
        ],
      })
      .mockRejectedValueOnce(new Error("raw secret failure"));

    renderApp({ checkStatus });
    expect(
      await screen.findByText("Input could not be read"),
    ).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Retry" }));

    expect(await screen.findByText("Unexpected failure")).toBeInTheDocument();
    expect(
      screen.getByText("The desktop action failed unexpectedly."),
    ).toBeInTheDocument();
    expect(screen.queryByText(/raw secret failure/i)).not.toBeInTheDocument();
  });

  it("renders ordered structured action errors and correlation", async () => {
    const engineStatusClient: EngineStatusClient = {
      checkStatus: () =>
        Promise.reject({
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
        }),
    };

    renderApp(engineStatusClient);

    expect(
      await screen.findByText("Check the supplied values"),
    ).toBeInTheDocument();
    const messages = screen.getAllByRole("listitem");
    expect(messages).toHaveLength(2);
    expect(messages[0]!).toHaveTextContent(
      "The first fixture value is invalid.",
    );
    expect(messages[1]!).toHaveTextContent(
      "The second fixture value conflicts with current state.",
    );
    expect(screen.getByRole("button", { name: "Retry" })).toBeInTheDocument();
  });

  it("renders structured action errors without unavailable correlation", async () => {
    const engineStatusClient: EngineStatusClient = {
      checkStatus: () =>
        Promise.reject({
          kind: "operation",
          errors: [
            {
              type: "Validation",
              code: "protocol.invalidRequest",
              message: "The request does not match the engine protocol schema.",
            },
          ],
        }),
    };

    renderApp(engineStatusClient);

    expect(
      await screen.findByText("Check the supplied values"),
    ).toBeInTheDocument();
    expect(
      screen.getByText(
        "The request does not match the engine protocol schema.",
      ),
    ).toBeInTheDocument();
    expect(screen.queryByText(/^Request /)).not.toBeInTheDocument();
  });

  it("sanitizes an unknown client rejection", async () => {
    const engineStatusClient: EngineStatusClient = {
      checkStatus: async () => {
        throw new Error("sensitive raw rejection");
      },
    };

    renderApp(engineStatusClient);

    expect(await screen.findByText("Unexpected failure")).toBeInTheDocument();
    expect(
      screen.getByText("The desktop action failed unexpectedly."),
    ).toBeInTheDocument();
    expect(
      screen.queryByText(/sensitive raw rejection/i),
    ).not.toBeInTheDocument();
  });

  it("retries readiness without reloading", async () => {
    const user = userEvent.setup();
    const checkStatus = vi
      .fn()
      .mockRejectedValueOnce({
        kind: "operation",
        errors: [
          {
            type: "Timeout",
            code: "engine.responseTimedOut",
            message: "Timed out.",
          },
        ],
      })
      .mockResolvedValueOnce(undefined);

    renderApp({ checkStatus });
    await user.click(await screen.findByRole("button", { name: "Retry" }));

    expect(checkStatus).toHaveBeenCalledTimes(2);
    expect(
      await screen.findByRole("dialog", { name: "Open a repository" }),
    ).toBeInTheDocument();
  });
});

function renderApp(engineStatusClient: EngineStatusClient) {
  return render(
    <App
      engineStatusClient={engineStatusClient}
      repositoryClient={repositoryClient}
      repositoryFolderPicker={repositoryFolderPicker}
    />,
  );
}
