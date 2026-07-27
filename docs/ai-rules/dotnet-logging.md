# Logging

Read before adding or changing any backend diagnostics, log statement, or logging configuration.

Logging is an important part of ChangeLens correctness, supportability, and auditability. Preserve these practices whenever backend behavior is added or changed:

- Inject `ILogger<T>` into engine services. Do not use `Console`, static loggers, or provider-specific logger types for application diagnostics.
- Configure logging providers only at the Engine composition boundary. Engine and Infrastructure implementations use the Microsoft logging abstraction when logging is required; keep Core domain logic logging-free unless a concrete cross-boundary need is demonstrated.
- Keep standard output reserved exclusively for versioned engine protocol messages. Console diagnostics must go to standard error, and rolling local-file logging must remain available.
- Use structured message templates with stable, descriptive property names. Do not use string interpolation to build log messages.
- At `Information`, record meaningful lifecycle and operation outcomes with available correlation identifiers, method or operation names, stable error codes, and elapsed time. Use `Debug` for detailed diagnostic payloads and `Warning` or higher for degraded or unexpected conditions.
- Never log secrets, credentials, unrestricted source content, or other sensitive data. Raw protocol payloads may be logged only at `Debug`, only when their schema has been reviewed for sensitive fields, and must be removed or redacted when that assumption changes.
- Log expected failures once at the outer boundary where sufficient context exists, without adding side effects to Result forwarding. Log unexpected exceptions once at the exception boundary and include the exception object.
- Add or update tests for logging behavior that is part of a process or protocol contract, especially the rule that diagnostics cannot pollute standard output.
