import { createRequire } from "node:module";

const require = createRequire(
  new URL("./src/desktop/package.json", import.meta.url),
);
const eslint = require("@eslint/js");
const reactHooks = require("eslint-plugin-react-hooks");
const reactRefresh = require("eslint-plugin-react-refresh").default;
const globals = require("globals");
const tseslint = require("typescript-eslint");

export default tseslint.config(
  {
    ignores: [
      "**/coverage/**",
      "**/dist/**",
      "**/mockups/**",
      "**/node_modules/**",
    ],
  },
  eslint.configs.recommended,
  ...tseslint.configs.recommended,
  reactHooks.configs.flat["recommended-latest"],
  reactRefresh.configs.vite,
  {
    files: [
      "eslint.config.mjs",
      "src/desktop/scripts/**/*.mjs",
      "src/desktop/ui/**/*.{js,ts,tsx}",
      "tests/unit/desktop/**/*.{js,ts,tsx}",
    ],
    languageOptions: {
      ecmaVersion: 2023,
      globals: {
        ...globals.browser,
        ...globals.node,
      },
    },
  },
);
