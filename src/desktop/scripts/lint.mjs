import { fileURLToPath } from "node:url";
import { ESLint } from "eslint";

const repositoryRoot = fileURLToPath(new URL("../../..", import.meta.url));
const eslint = new ESLint({
  cwd: repositoryRoot,
  overrideConfigFile: "eslint.config.mjs",
});
const results = await eslint.lintFiles([
  "eslint.config.mjs",
  "src/desktop/scripts",
  "src/desktop/ui",
  "tests/unit/desktop",
]);
const formatter = await eslint.loadFormatter("stylish");
const output = formatter.format(results);

if (output.length > 0) {
  process.stdout.write(output);
}

if (results.some((result) => result.errorCount > 0)) {
  process.exitCode = 1;
}
