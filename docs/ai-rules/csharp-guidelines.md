# C# Guidelines

Read before writing or changing any C# code under `src/engine` or `tests`.

These rules extend the language-neutral organization rules in `core-principles.md`.

- Every injected domain, Core, application, Infrastructure, provider, adapter, or process-boundary service must depend on
  and be registered through a capability-specific interface. Concrete classes may be injected only when they are immutable
  value models, framework-owned types, or genuine stateless language-level utilities for which an interface would add no
  boundary. Do not inject concrete service implementations merely because there is currently one implementation.
- Avoid static service classes. Static classes are acceptable only for genuine constants or stateless language-level utilities when an injected service would add ceremony without a substitutable boundary.
- Keep production executable `Program.cs` files limited to host creation, one named composition extension call,
  host construction and disposal, and one named run extension call. Put service registration, configuration, and
  lifecycle or exception orchestration in capability-owned extension methods.
- In C# code, explicitly qualify instance members with `this.`. Apply this to instance fields, properties, methods,
  events, and other member access even when the qualification is not required for disambiguation.

## Formatting and readability

Use compact-first formatting for hand-authored production Engine C# with a hard maximum of 150 characters per physical
line, including indentation. Tabs are not permitted. XML documentation and ordinary comments follow the same width;
only an indivisible literal such as SQL, a URI, a hash, a regular expression, or exact protocol/fixture text may exceed
the limit, and only on the literal-bearing line.

Keep a complete, straightforward operation on one line when it fits: signatures, primary constructors, invocations,
object construction, assignments, local declarations, `using var`, `return` and `throw` expressions, logging calls,
expression-bodied members, simple collection expressions and initializer entries, and simple Boolean conditions or catch
filters. Existing line breaks do not justify keeping a construct multiline. Never put multiple statements on one line;
braced blocks retain the normal multiline brace style.

When a declaration or invocation exceeds 150 characters, or contains a block-bodied lambda or control-flow body, keep
the member name and opening parenthesis on the first line, put one argument or parameter on each continuation line,
indent continuation lines by four spaces relative to the containing statement, and keep the closing parenthesis beside
the final item. Do not partially pack arguments or parameters. Wrap at the shallowest useful syntax boundary: do not
expand nested invocations merely because an inner argument list needs wrapping. Keep a compact nested subexpression on
one line when it fits.

For other multiline expressions, put ternary arms on separate lines with `?` and `:` leading the continuation lines,
put one Boolean operand per line with the operator leading the continuation line, break fluent chains before the dot,
and put one initializer entry or long collection element on each continuation line.

For XML documentation, keep a complete tag on one line when it fits and the XML documentation rules permit it. Reflow
prose at natural phrase boundaries before 150 columns, preserve the established four-space XML content indentation in
multiline tags, and never change documentation meaning merely to reduce line count.

Use blank lines to mark transitions between logical phases, not to spread code visually. Keep declarations that prepare
one operation adjacent, keep a produced result beside its guard, separate independent operations with exactly one blank
line, and keep consecutive validation guards together. Do not add blank lines after an opening brace or before a closing
brace, never keep multiple consecutive blank lines, and keep one blank line between type members. Declare locals near
their first use; do not collect unrelated locals at the start of a method.

Order type members by accessibility and then static placement:

```text
constants
fields
constructors
properties
--- methods ---
public
public static
protected
protected static
internal
internal static
private
private static
```

### Engine composition

`EngineHostApplicationBuilderExtensions.AddEngine` remains the single public composition entry point. It performs the
null guard, configures service-provider build and scope validation, adds logging, and invokes private helpers in
capability order:

```csharp
ArgumentNullException.ThrowIfNull(builder);

builder.ConfigureContainer(
    new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }),
    static _ => { });
builder.AddEngineLogging();

AddRuntimeServices(builder);
AddLocalStateServices(builder);
AddPreferenceServices(builder);
AddEngineStatusServices(builder);
AddRepositoryServices(builder);
AddComparisonServices(builder);
AddProtocolServices(builder);
AddActionHandlers(builder);
ValidateActionHandlerRegistrations(builder.Services);
```

Keep the helpers in the same file. Registration helpers remain private;
`ValidateActionHandlerRegistrations` is internal so the Engine integration suite can exercise the production invariant
without reflection. Do not introduce new extension classes, interfaces, nested types, or decorative region/comment
separators. The helpers register:

- `AddRuntimeServices`: `TextReader`, `TextWriter`, and `TimeProvider`.
- `AddLocalStateServices`: singleton `LocalStatePaths`; scoped `DbContextOptions<ChangeLensLocalStateDbContext>`,
  `ChangeLensLocalStateDbContext`, `ILocalStateInitializer`, `IRepositoryHistoryStore`, and
  `IRepositoryHistoryService`. Configure SQLite through `AddDbContext` here.
- `AddPreferenceServices`: `IColorThemePreferenceStore` and `IColorThemePreferenceService`.
- `AddEngineStatusServices`: `IEngineStatusService`.
- `AddRepositoryServices`: `ICanonicalRepositoryPathKeyProvider`, `IRepositoryPathResolver`, `IGitCommandRunner`, and
  `IGitRepositoryInspector`.
- `AddComparisonServices`: `IComparisonFileSummaryComposer`, `IGitComparisonTargetDiscovery`, `IGitComparisonPreparer`,
  `IGitComparisonFreshnessChecker`, `IGitRemoteBaselineTracker`, and `IComparisonTargetPageBuilder`.
- `AddProtocolServices`: `IEngineProtocolSerializer`, `IEngineProtocolTransport`, and `EngineProtocolHost` as the hosted
  service.
- `AddActionHandlers`: every `IActionHandler` implementation, one `AddActionHandler<THandler>` line per approved
  protocol action. The helper registers the handler as a keyed scoped service under its static declared action. Keep the
  twelve registrations contiguous in this helper rather than distributing them across the capability helpers.

`TextReader`, `TextWriter`, `TimeProvider`, `IEngineProtocolSerializer`, `IEngineProtocolTransport`,
`EngineProtocolHost`, and `LocalStatePaths` are singleton. All services that serve a protocol request are scoped.
Validate the action descriptors as an exact one-to-one match with `EngineActionConstants.ApprovedActions` after
registration and before building the provider. Keep logging initialized before services that log and register the
protocol hosted service before the contiguous action-handler registrations.

## Related rules

- Expected failures and error propagation: `dotnet-results.md`.
- Diagnostics and log levels: `dotnet-logging.md`.
- XML documentation comments: `csharp-xml-documentation.md`.
- Test placement and isolation: `testing.md`.
