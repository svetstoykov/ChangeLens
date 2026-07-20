import { fileURLToPath, URL } from "node:url";
import react from "@vitejs/plugin-react";
import { defineConfig } from "vitest/config";

const tauriDevelopmentHost = process.env.TAURI_DEV_HOST;
const desktopTests = fileURLToPath(
  new URL("../../../tests/unit/desktop", import.meta.url),
);
const repositoryRoot = fileURLToPath(new URL("../../..", import.meta.url));

export default defineConfig({
  clearScreen: false,
  plugins: [react()],
  resolve: {
    alias: {
      "@": fileURLToPath(new URL("./src", import.meta.url)),
    },
    dedupe: [
      "@testing-library/jest-dom",
      "@testing-library/react",
      "@testing-library/user-event",
      "@tauri-apps/api",
      "react",
      "react-dom",
      "vitest",
    ],
  },
  server: {
    port: 5173,
    strictPort: true,
    host: tauriDevelopmentHost || false,
    hmr: tauriDevelopmentHost
      ? {
          protocol: "ws",
          host: tauriDevelopmentHost,
          port: 5174,
        }
      : undefined,
    watch: {
      ignored: ["**/src-tauri/**"],
    },
    fs: process.env.VITEST ? { allow: [repositoryRoot] } : undefined,
  },
  envPrefix: ["VITE_", "TAURI_ENV_*"],
  build: {
    target:
      process.env.TAURI_ENV_PLATFORM === "windows" ? "chrome105" : "safari13",
    minify: process.env.TAURI_ENV_DEBUG ? false : "oxc",
    sourcemap: Boolean(process.env.TAURI_ENV_DEBUG),
  },
  test: {
    environment: "jsdom",
    include: [`${desktopTests}/**/*.test.{ts,tsx}`],
    coverage: {
      provider: "v8",
      reporter: ["text", "html"],
    },
  },
});
