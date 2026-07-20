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

  it("reports when the desktop engine cannot be reached", async () => {
    const engineClient: EngineClient = {
      getInformation: async () => {
        throw new Error("Tauri IPC is unavailable");
      },
    };

    render(<App engineClient={engineClient} />);

    expect(
      await screen.findByText("Desktop engine unavailable"),
    ).toBeInTheDocument();
    expect(screen.getByRole("status")).toHaveAttribute("data-state", "error");
  });
});
