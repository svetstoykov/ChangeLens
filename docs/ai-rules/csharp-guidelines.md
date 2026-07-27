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

## Related rules

- Expected failures and error propagation: `dotnet-results.md`.
- Diagnostics and log levels: `dotnet-logging.md`.
- XML documentation comments: `csharp-xml-documentation.md`.
- Test placement and isolation: `testing.md`.
