import "@testing-library/jest-dom/vitest";
import { cleanup, render, screen } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { createResolvablePromise } from "../Support/createResolvablePromise";
import { App } from "../../../../src/desktop/ui/src/App";
import type { EngineClient } from "../../../../src/desktop/ui/src/EngineInformation/Interfaces/EngineClient";

afterEach(cleanup);

describe("engine information", () => {
  it("shows a connection state while the engine request is pending", () => {
    const pendingInformation = createResolvablePromise<never>();
    const engineClient: EngineClient = {
      getInformation: () => pendingInformation.promise,
    };

    render(<App engineClient={engineClient} />);

    expect(
      screen.getByText("Connecting to ChangeLens.Engine…"),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveAttribute(
      "data-state",
      "connecting",
    );
  });

  it("shows information returned by the engine", async () => {
    const engineClient: EngineClient = {
      getInformation: async () => ({
        name: "ChangeLens.Engine",
        version: "0.1.0",
        protocolVersion: 1,
      }),
    };

    render(<App engineClient={engineClient} />);

    expect(
      await screen.findByText("ChangeLens.Engine 0.1.0 · protocol v1"),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveAttribute("data-state", "ready");
  });

  it("renders ordered structured action errors and correlation", async () => {
    const engineClient: EngineClient = {
      getInformation: () =>
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

    render(<App engineClient={engineClient} />);

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
    expect(screen.getByText("Request desktop-43")).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveAttribute("data-state", "error");
  });

  it("sanitizes an unknown client rejection", async () => {
    const engineClient: EngineClient = {
      getInformation: async () => {
        throw new Error("sensitive raw rejection");
      },
    };

    render(<App engineClient={engineClient} />);

    expect(await screen.findByText("Unexpected failure")).toBeInTheDocument();
    expect(
      screen.getByText("The desktop action failed unexpectedly."),
    ).toBeInTheDocument();
    expect(
      screen.queryByText(/sensitive raw rejection/i),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveAttribute("data-state", "error");
  });
});
