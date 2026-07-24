import { readdirSync, readFileSync } from "node:fs";
import { join, relative, resolve } from "node:path";
import * as ts from "../../../../src/desktop/node_modules/typescript/lib/typescript.js";
import { describe, expect, it } from "vitest";

const productionRoot = resolve(process.cwd(), "src");
const explicitCommands = [
  "engine_check_status",
  "select_repository_folder",
  "repository_open",
  "comparison_list_targets",
  "comparison_prepare",
  "comparison_check_freshness",
] as const;

describe("React comparison boundary", () => {
  it("keeps repository, protocol, persistence, logging, and network capabilities out of production React", () => {
    const productionSources = sourceFiles(productionRoot);
    const prohibitedPatterns = [
      /node:fs|child_process|Deno\.read|Bun\.file/,
      /protocolVersion|comparisons\.(listTargets|prepare|checkFreshness)|repositories\.open/,
      /localStorage|sessionStorage|indexedDB/,
      /\bfetch\s*\(/,
      /console\.(log|info|warn|error|debug)\s*\(/,
    ];

    for (const sourcePath of productionSources) {
      const source = readFileSync(sourcePath, "utf8");
      for (const pattern of prohibitedPatterns) {
        expect(
          source,
          `${relative(productionRoot, sourcePath)} must not match ${pattern}`,
        ).not.toMatch(pattern);
      }

      if (
        ![
          "Actions/Models/ActionError.ts",
          "Actions/Models/ActionErrorPresentation.ts",
          "Actions/Services/normalizeActionError.ts",
          "Actions/Services/presentActionError.ts",
        ].includes(relative(productionRoot, sourcePath))
      ) {
        expect(
          source,
          `${relative(productionRoot, sourcePath)} must not use correlation identifiers`,
        ).not.toMatch(/requestId/);
      }
    }
  });

  it("uses the exact fixed Tauri command multiset in typed clients", () => {
    const commandOccurrences = new Map<string, string[]>();

    for (const sourcePath of sourceFiles(productionRoot)) {
      const source = readFileSync(sourcePath, "utf8");
      for (const commandName of tauriInvokeCommands(source, sourcePath)) {
        commandOccurrences.set(commandName, [
          ...(commandOccurrences.get(commandName) ?? []),
          relative(productionRoot, sourcePath),
        ]);
      }
    }

    expect([...commandOccurrences.keys()].sort()).toEqual(
      [...explicitCommands].sort(),
    );
    expect(Object.fromEntries(commandOccurrences)).toEqual({
      engine_check_status: ["EngineStatus/Services/TauriEngineStatusClient.ts"],
      select_repository_folder: [
        "Repositories/Services/TauriRepositoryFolderPicker.ts",
      ],
      repository_open: ["Repositories/Services/TauriRepositoryClient.ts"],
      comparison_list_targets: [
        "Comparisons/Services/TauriComparisonClient.ts",
      ],
      comparison_prepare: ["Comparisons/Services/TauriComparisonClient.ts"],
      comparison_check_freshness: [
        "Comparisons/Services/TauriComparisonClient.ts",
      ],
    });
  });
});

function sourceFiles(directory: string): string[] {
  return readdirSync(directory, { withFileTypes: true }).flatMap((entry) => {
    const path = join(directory, entry.name);
    if (entry.isDirectory()) {
      return sourceFiles(path);
    }

    return /\.(ts|tsx)$/.test(entry.name) ? [path] : [];
  });
}

function tauriInvokeCommands(source: string, sourcePath: string): string[] {
  const file = ts.createSourceFile(
    sourcePath,
    source,
    ts.ScriptTarget.Latest,
    true,
  );
  const invokeNames = new Set<string>();
  const namespaces = new Set<string>();
  for (const statement of file.statements) {
    if (
      !ts.isImportDeclaration(statement) ||
      statement.moduleSpecifier.getText(file) !== '"@tauri-apps/api/core"' ||
      statement.importClause === undefined
    ) {
      continue;
    }

    const bindings = statement.importClause.namedBindings;
    if (bindings !== undefined && ts.isNamespaceImport(bindings)) {
      namespaces.add(bindings.name.text);
    }
    if (bindings !== undefined && ts.isNamedImports(bindings)) {
      for (const item of bindings.elements) {
        if ((item.propertyName?.text ?? item.name.text) === "invoke") {
          invokeNames.add(item.name.text);
        }
      }
    }
  }

  const commands: string[] = [];
  const visit = (node: ts.Node) => {
    if (ts.isCallExpression(node) && isTauriInvoke(node.expression)) {
      const firstArgument = node.arguments[0];
      if (
        firstArgument === undefined ||
        (!ts.isStringLiteral(firstArgument) &&
          !ts.isNoSubstitutionTemplateLiteral(firstArgument))
      ) {
        throw new Error(`${sourcePath} invokes Tauri with a dynamic command.`);
      }
      commands.push(firstArgument.text);
    }
    ts.forEachChild(node, visit);
  };
  const isTauriInvoke = (expression: ts.Expression): boolean =>
    ts.isIdentifier(expression)
      ? invokeNames.has(expression.text)
      : ts.isPropertyAccessExpression(expression) &&
        expression.name.text === "invoke" &&
        ts.isIdentifier(expression.expression) &&
        namespaces.has(expression.expression.text);

  ts.forEachChild(file, visit);
  return commands;
}
