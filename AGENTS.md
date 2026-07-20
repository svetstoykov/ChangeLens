# ChangeLens Repository Instructions

## Product and Technology

ChangeLens is a local desktop change-intelligence application. It combines deterministic repository tooling with optional AI reasoning to explain software changes, reveal wider impact, and present evidence-backed findings to developers.

The initial technology stack is:

- Tauri 2 for the desktop shell, packaging, updates, and operating-system integration.
- React, TypeScript, and Vite for the user interface.
- Monaco Editor for read-only source and diff presentation.
- .NET 10 for the local analysis engine.
- SQLite and local artifact files for persistence.
- The installed Git CLI for source-control facts.
- Roslyn and MSBuild for the first deep repository adapter.
- A ChangeLens-owned, provider-neutral AI contract.

Keep detailed and frequently changing product requirements in `docs/product` or feature-specific specifications. Keep this file focused on durable engineering direction.

## Superpowers Skills

Before invoking any `superpowers:*` skill, ask the user for permission and briefly explain why the skill applies. Invoke the skill only after the user explicitly approves its use. If approval is not granted, continue without that skill when possible.

## Repository Structure

```text
change_lens/
├── .github/workflows/
├── build/
│   ├── packaging/
│   └── scripts/
├── contracts/engine-protocol/
├── docs/
│   ├── architecture/
│   ├── decisions/
│   ├── evaluation/
│   └── product/
├── src/
│   ├── desktop/
│   │   ├── ui/
│   │   └── src-tauri/
│   └── engine/
│       ├── ChangeLens.Core/
│       ├── ChangeLens.Infrastructure/
│       └── ChangeLens.Engine/
└── tests/
    ├── unit/
    │   ├── desktop/
    │   └── engine/
    └── integration/
        ├── desktop/
        └── engine/
```

Do not add speculative capability, adapter, or provider folders. Create a folder when its first real implementation is added.

## Architecture

### Engine projects

- `ChangeLens.Core` contains domain concepts, invariants, transport-independent Results, and interfaces required from external capabilities. It has no references to other ChangeLens projects. External NuGet packages are permitted when they preserve this ownership boundary.
- `ChangeLens.Infrastructure` implements Core interfaces for Git, SQLite, local artifacts, filesystem access, subprocess execution, Roslyn/MSBuild analysis, and configured AI providers. It references Core.
- `ChangeLens.Engine` is the executable application boundary. It owns use-case orchestration, dependency-injection composition, lifecycle, and versioned standard-input/output protocol handling. It references Core and Infrastructure.

The dependency direction is:

```text
ChangeLens.Infrastructure -> ChangeLens.Core
ChangeLens.Engine         -> ChangeLens.Core + ChangeLens.Infrastructure
```

Do not add an Application project without a demonstrated second host or another concrete need for transport-independent orchestration. The Engine currently fills the composition and application-boundary role that an API project fills in a web application.

### Desktop and process boundaries

The Tauri layer must remain thin. It may manage the native window, engine process, approved native capabilities, and command/event relay. It must not contain repository-analysis or product-domain logic.

The React interface must not inspect arbitrary repository files or execute engineering tools directly. Repository access and tool execution belong to the Engine, where permissions, limits, evidence capture, and auditability can be applied consistently.

The engine protocol is a versioned product boundary:

- Standard output carries protocol messages only.
- Standard error carries diagnostics.
- Requests, progress, cancellation, known failures, unexpected failures, and results use explicit structured messages.
- Exceptions never cross a process boundary directly.
- Protocol schemas belong in `contracts/engine-protocol`.

## Design and Organization Rules

Apply these principles in priority order:

1. Simplicity: keep implementation and interfaces simple; interface simplicity has priority.
2. Correctness: all observable behavior must be correct.
3. Consistency: prefer a slightly less simple design over an inconsistent one.
4. Completeness: cover reasonably expected cases without using simplicity as an excuse for important gaps.

Organize production and test code as vertical slices: product capability first, technical role second.

```text
ChangeLens.Core/
└── AnalysisRuns/
    ├── Interfaces/
    ├── Models/
    └── Services/
```

- Mirror a capability path across Core, Engine, Infrastructure, and tests when that capability spans those boundaries.
- Never create project-wide `Models`, `Services`, or `Interfaces` dumping grounds.
- Use interfaces and dependency injection for replaceable behavior and external boundaries.
- Do not create an interface mechanically for every class. Immutable models, value objects, and behaviorless helpers do not need one.
- Avoid static service classes. Static classes are acceptable only for genuine constants or stateless language-level utilities when an injected service would add ceremony without a substitutable boundary.
- Keep one primary type per file.
- Do not use nested classes. Put each class in its own appropriately named file.
- Do not use decorative region or comment separators such as `// --------`.
- Put stable non-prose literals such as protocol identifiers, property names, error codes, configuration keys,
  file-name patterns, and process exit codes in a capability-specific `Constants` folder. Use a static class named
  for its scope, such as `EngineProtocolConstants`; do not create a project-wide constants dumping ground.
- Keep one-off human-readable messages and structured logging message templates at their call sites unless they
  are reused or form part of a stable external contract.
- Keep production executable `Program.cs` files limited to host creation, one named composition extension call,
  host construction and disposal, and one named run extension call. Put service registration, configuration, and
  lifecycle or exception orchestration in capability-owned extension methods.
- Prefer the smallest design that fully preserves correctness, consistency, and important behavior.

## Validation and Result Architecture

The backend uses transport-independent Results for expected validation, domain, and known infrastructure failures.

```text
validator or repository
    -> Result / Result<T> with OperationError
    -> service checks IsFailure
    -> error is returned or forwarded without reclassification
    -> outer application boundary maps ErrorType
       and preserves the stable OperationError.Code
```

Follow these consistency rules when the Result types are implemented:

- Use `Result` for payload-free success and `Result<T>` for operations that return data.
- Represent success by the absence of errors. Callers must inspect `IsSuccess` or `IsFailure`; nullable or default `Data` does not determine success.
- Let `OperationError` carry a human-readable message, a broad transport-independent `ErrorType`, and an optional stable machine-readable code.
- Select stable error codes where a failure originates and preserve them unchanged across layers.
- Compose explicitly: call the operation, inspect `IsFailure`, then return or forward the error before using success data or performing later effects.
- Return a failed Result directly when its return type is compatible. When the payload type changes, propagate the failure with `Result.ErrorFromResult` or `Result.ErrorFromResult<T>` instead of reconstructing, wrapping, translating, or dropping errors.
- Treat the forwarding helpers as lossless failure propagation: they preserve the source errors' message, `ErrorType`, optional code, object identity, and order while discarding payload and success-message data. They do not log or otherwise cause side effects.
- Keep callers independent from lower-layer error codes. Only the operation that detects a specific condition assigns a code; intermediate layers propagate it unchanged unless they deliberately translate to a different abstraction.
- Keep forwarding transport-independent and free of logging or other side effects.
- Let the outer application boundary translate error categories into its protocol-specific representation and add correlation information.
- Keep the implementation deliberately small. Do not add `Bind`, `Map`, result builders, implicit failure conversion, or a result-specific extension-method framework.
- Treat expected failures as Result data. Treat unexpected exceptions through the separate exception boundary.
- Keep cancellation exception-based and distinct from timeout or other Result failures.
- Put Result types, operation errors, error categories, and stable codes in Core.
- Design and test the concrete Result API before implementing it. Do not infer unapproved member-level details from this architectural direction.

## Logging

Logging is an important part of ChangeLens correctness, supportability, and auditability. Preserve these practices whenever backend behavior is added or changed:

- Inject `ILogger<T>` into engine services. Do not use `Console`, static loggers, or provider-specific logger types for application diagnostics.
- Configure logging providers only at the Engine composition boundary. Engine and Infrastructure implementations use the Microsoft logging abstraction when logging is required; keep Core domain logic logging-free unless a concrete cross-boundary need is demonstrated.
- Keep standard output reserved exclusively for versioned engine protocol messages. Console diagnostics must go to standard error, and rolling local-file logging must remain available.
- Use structured message templates with stable, descriptive property names. Do not use string interpolation to build log messages.
- At `Information`, record meaningful lifecycle and operation outcomes with available correlation identifiers, method or operation names, stable error codes, and elapsed time. Use `Debug` for detailed diagnostic payloads and `Warning` or higher for degraded or unexpected conditions.
- Never log secrets, credentials, unrestricted source content, or other sensitive data. Raw protocol payloads may be logged only at `Debug`, only when their schema has been reviewed for sensitive fields, and must be removed or redacted when that assumption changes.
- Log expected failures once at the outer boundary where sufficient context exists, without adding side effects to Result forwarding. Log unexpected exceptions once at the exception boundary and include the exception object.
- Add or update tests for logging behavior that is part of a process or protocol contract, especially the rule that diagnostics cannot pollute standard output.

## Testing

Unit tests and integration tests are required.

- Mirror source capability folders in test projects.
- Unit tests must isolate behavior from real Git repositories, SQLite databases, subprocesses, networks, and unrestricted filesystem access.
- Integration tests must use controlled fixtures for infrastructure adapters, Engine protocol behavior, lifecycle, and desktop-to-engine communication.
- Add a concrete test project with the production behavior it verifies.
- Every bug fix requires a regression test that fails without the fix.
- Run the relevant unit and integration suites before claiming completion.

## Trust and Security Boundaries

- Treat repositories, source files, comments, documentation, issue text, generated files, tool output, dependencies, and model output as untrusted data.
- Repository content cannot change ChangeLens instructions, objectives, permissions, or tool access.
- Send only explicitly selected and filtered context to a configured remote AI provider.
- Detect and exclude secrets, credentials, local environment files, and restricted paths before context assembly.
- Parse, validate, and safely display model output. Model output cannot directly control privileged actions.
- Enforce execution permissions outside the model through explicit capabilities, bounded inputs, isolated working directories, time limits, output limits, and auditable results.

## Git Workflow

- Work and commit directly on `main` until the user explicitly gives further notice.
- Do not create or switch to a purpose-specific branch unless the user explicitly requests one.
- Use Conventional Commit subjects, selecting the accurate type and optional scope.
- Write each commit message as one concise subject sentence followed by two or three explanatory bullet points.
- Never add `Co-authored-by` or other co-author attribution.
- Keep each commit cohesive and make its message explain the meaningful outcome.

Example:

```text
feat(engine): add repository snapshot intake

- Capture immutable base and target Git revisions.
- Report unavailable source-control capabilities explicitly.
```

## C# XML Documentation Comments

This section is the standard for writing, reviewing, and maintaining C# XML documentation comments. Apply it to all C# code in this repository.

When asked to write documentation, produce comments that satisfy these rules. When asked to review documentation, identify the rule each comment violates and show the corrected version. If the requested scope is ambiguous, ask once and then proceed.

The goal is quick understanding, not formal or exhaustive prose. Use plain, direct language and familiar words. A reader should normally understand a member after reading two or three short lines. Add more detail only when it explains behavior, context, or a constraint that matters.

### Tag taxonomy

| Tag | Purpose | Required on |
| --- | --- | --- |
| `<summary>` | One-sentence “what” | Every non-private member; private members according to the policy below |
| `<remarks>` | “Why” and “how”; architectural context, side effects, and constraints | Types, complex methods, and non-obvious properties |
| `<param name="...">` | What the parameter represents and its constraints | Every documented method or constructor parameter |
| `<typeparam name="...">` | What the type parameter represents | Every generic type or method |
| `<returns>` | What the return value represents | Every documented non-void method |
| `<value>` | What a property value represents when the summary is insufficient | Optional for properties with non-obvious values |
| `<exception cref="...">` | The condition under which an exception is thrown | Every explicitly thrown exception |
| `<inheritdoc />` | Documentation inherited from a base or interface member | Overrides and interface implementations whose contract is unchanged |
| `<see cref="...">` | A cross-reference to a type or member | Inline wherever precision requires it |
| `<see langword="..."/>` | A C# keyword such as `null`, `true`, `false`, `async`, or `await` | Always; never use backticks for keywords in XML comments |
| `<paramref name="..."/>` | An inline parameter reference | Inside parameter, remarks, or exception prose |
| `<para>` | A distinct idea inside remarks | Whenever remarks contain more than one thought |
| `<example>` and `<code>` | A usage demonstration | Entry-point APIs whose usage is not obvious |

Documentation must be well-formed XML. Keep every `cref` and `name` attribute accurate so compiler validation succeeds.

### Canonical phrasing

Use the established patterns below. Keep the wording natural and replace stock phrases when a simpler explanation is clearer.

#### Constructors

```csharp
/// <summary>
///     Initializes a new instance of the <see cref="PermissionScanner" /> class.
/// </summary>
```

#### Boolean properties

```csharp
/// <summary>
///     Gets or sets a value indicating whether discovered permissions are cached between scans.
/// </summary>
/// <value>
///     <see langword="true" /> if results are cached; otherwise, <see langword="false" />.
///     The default is <see langword="true" />.
/// </value>
```

#### Other properties

Begin with “Gets” or “Gets or sets” and state the default when one exists.

```csharp
/// <summary>
///     Gets the namespace into which generated permission constants are emitted.
/// </summary>
```

#### Methods

Use a third-person present-tense verb. Do not begin with “This method”.

```csharp
/// <summary>
///     Scans the given assembly for types decorated with permission attributes.
/// </summary>
```

#### Async methods

Lead with “Asynchronously”. The return documentation always follows the task-result formula.

```csharp
/// <summary>
///     Asynchronously scans the given assembly for permission declarations.
/// </summary>
/// <returns>
///     A task that represents the asynchronous operation. The task result contains
///     the set of discovered <see cref="PermissionDescriptor" /> instances.
/// </returns>
```

For a plain `Task`, use: `A task that represents the asynchronous operation.`

Include:

```csharp
/// <param name="cancellationToken">
///     A <see cref="CancellationToken" /> to observe while waiting for the task to complete.
/// </param>
```

Where applicable, include:

```csharp
/// <exception cref="OperationCanceledException">
///     If the <see cref="CancellationToken" /> is canceled.
/// </exception>
```

#### Fluent builders

```csharp
/// <returns>The same builder instance so that multiple calls can be chained.</returns>
```

#### Events

```csharp
/// <summary>
///     Occurs when a duplicate permission key is detected during generation.
/// </summary>
```

#### Types

Use “Represents” for domain objects, “Defines” for contracts and enums, and “Provides” for static helper classes.

```csharp
/// <summary>
///     Represents a single discovered permission in the <c>{domain}.{resource}.{action}</c> convention.
/// </summary>
```

#### Enums

Document both the enum type and every member. Member summaries are noun phrases and do not begin with “Indicates that”.

```csharp
/// <summary>
///     Defines the strategies for resolving duplicate permission keys.
/// </summary>
public enum DuplicateKeyStrategy
{
    /// <summary>
    ///     The first declaration wins; later duplicates are ignored.
    /// </summary>
    FirstWins,
}
```

#### Overrides and interface implementations

Use `<inheritdoc />` when the contract is unchanged. If the implementation adds notable behavior, retain `<inheritdoc />` and append remarks.

```csharp
/// <inheritdoc />
/// <remarks>
///     This implementation caches results per assembly; see <see cref="ClearCache" />.
/// </remarks>
```

### Core XML documentation rules

#### Summary

Write one complete sentence answering “what does this do?”. End it with a period. Use plain words, keep it short, and say what would help a reader understand the member. Do not explain implementation details or repeat the member name. Indent summary text four spaces inside the tag unless the surrounding codebase has an established different style.

#### Remarks

Use remarks to answer why a member exists and how it behaves. Keep the explanation direct and include only context that helps someone use or maintain the code.

- Use one `<para>` for each distinct idea.
- Use the first paragraph for the system role or behavioral contract.
- Use later paragraphs for side effects, ordering constraints, thread safety, and lifetime.
- For dependency-injection services, state the lifetime and thread-safety contract explicitly.
- Link conceptual documentation with `<see href="...">` when relevant.
- Omit remarks on simple, self-evident members.

For example, adjust this lifetime contract to match the service:

```text
The implementation may depend on other services registered with any lifetime.
The implementation does not need to be thread-safe.
```

Use an XML link for conceptual documentation:

```csharp
/// See <see href="https://example.com/docs/permissions">Permission generation</see>
/// for more information and examples.
```

#### Parameters

Explain what each value represents and state its constraints. Keep the description short and use the same words a developer would use in conversation. State null contracts explicitly:

```csharp
/// <param name="key">The permission key. Cannot be <see langword="null" />.</param>
```

For optional null values, describe what null means:

```csharp
/// <param name="convention">
///     The naming convention, or <see langword="null" /> to use the default convention.
/// </param>
```

#### Return values

Explain what the resolved value represents, not merely its type. Prefer one short sentence unless an important condition needs another line.

```csharp
/// <returns>
///     The matching descriptor, or <see langword="null" /> if no permission with the given key exists.
/// </returns>
```

#### Exceptions

Use one `<exception>` element per exception type. State the condition in plain language. Separate multiple conditions for the same exception with `-or-`.

```csharp
/// <exception cref="ArgumentException">
///     <paramref name="key" /> is empty.
///     -or-
///     <paramref name="key" /> does not follow the
///     <c>{domain}.{resource}.{action}</c> convention.
/// </exception>
```

Document exceptions thrown by the member or observably by its direct callees. Do not document the entire transitive exception closure.

#### Cross-references

- Use `<see cref="..." />` for types and members.
- Use `<see langword="..." />` for C# keywords.
- Use `<c>...</c>` for inline literals and format conventions.
- Use `<code>` for multi-line samples.

#### Infrastructure APIs

For public-for-technical-reasons members, use this warning as the entire summary:

```csharp
/// <summary>
///     This is an internal API that supports the library infrastructure and is not
///     subject to the same compatibility standards as public APIs. It may be changed
///     or removed without notice in any release.
/// </summary>
```

### Required and private members

Document every non-private type and member. This includes `public`, `protected`, `internal`, `protected internal`, and `private protected` declarations. It applies to types, constructors, methods, properties, fields, constants, events, operators, delegates, and every enum member.

Private members are the only exception. Document a private member when:

- It implements specific or important functionality worth explaining.
- Its purpose or behavior is not obvious from its name and signature.
- A reader needs context to understand why it exists or how it should be changed.
- It implements a core algorithm or key invariant.
- It has important side effects, preconditions, ordering rules, recursion, caching, reentrancy, or shared-state changes.
- It exists because of a bug fix, workaround, security concern, compatibility rule, or performance decision that a future maintainer might undo. A plain `//` comment explaining why may be more suitable.
- It is a field with a non-obvious purpose, ownership rule, lifetime, unit, format, or allowed value.

Skip documentation when a private member is an obvious storage field, a trivial delegate-through, a small guard helper, or a self-explanatory expression-bodied member. Do not add documentation that merely restates the name or signature.

For a documented private member, apply the same XML structure and quality rules. Keep obvious parameter and return descriptions brief, but do not omit context that the reader needs.

```csharp
/// <summary>
///     Resolves attribute inheritance for the given type, walking base types depth-first.
/// </summary>
/// <remarks>
///     <para>
///         Declarations on the most-derived type win. The walk stops at the first type
///         outside the scanned assembly to avoid pulling in framework attributes.
///     </para>
///     <para>
///         This method mutates <see cref="_seenKeys" /> and must only be called while
///         holding <see cref="_scanLock" />.
///     </para>
/// </remarks>
private void ResolveInheritedPermissions(Type type)
{
}
```

### XML documentation review checklist

When reviewing documentation, report each issue as: **member → rule violated → corrected comment**.

Check each documented member in this order:

1. Every non-private member is documented, and private members that need explanation are documented.
2. The summary is one sentence, uses plain language and the correct verb form, ends with a period, and does not restate the name.
3. Canonical phrasing is used for constructors, Boolean properties, async methods, builders, events, enums, and types without making the wording needlessly formal.
4. Every parameter and type parameter is documented with its null or empty contract; every non-void return explains what the value represents.
5. Explicit exceptions are complete, their conditions are stated, and multiple conditions use `-or-`.
6. Keywords use `<see langword="..." />`, types use `<see cref="..." />`, and literals use `<c>`.
7. `<inheritdoc />` replaces copied base or interface documentation.
8. Remarks use `<para>` for distinct ideas, and implementation details do not leak into summaries.
9. Comments are short and easy to understand while still explaining the behavior and context that matter.
