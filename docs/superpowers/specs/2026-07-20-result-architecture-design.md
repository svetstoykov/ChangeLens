# Result Architecture Design

## Objective

Provide a small, transport-independent Core model for representing expected operation outcomes as data. The model supports validation, domain, and known infrastructure failures without coupling callers to the engine protocol, desktop UI, or a specific presentation technology.

## Scope

The implementation creates one `Results` vertical slice in `ChangeLens.Core` with these public types:

- `Result` for payload-free outcomes.
- `Result<T>` for successful outcomes with a typed payload.
- `OperationError` for immutable structured failure detail.
- `ErrorType` for broad, presentation-neutral failure categories.

The slice has no project or package dependencies and does not change the engine protocol or add presentation mapping.

## Result Model

`Result` owns a private `List<OperationError>`. `IsSuccess` is true only when that list is empty and `IsFailure` is its inverse. `Errors` returns a read-only view, and `SuccessMessage` provides optional text for a successful outcome. Its constructor is protected and the method that appends an error is private.

`Result<T>` inherits from `Result` and has a protected-internal constructor. It exposes `T? Data`, which is independent from success state: a successful result may contain `null` or a default value, while a failed typed result has default data. Consumers must inspect `IsSuccess` or `IsFailure` before consuming `Data`.

`Result` exposes `Success`, `Success<T>`, `Fail`, `Fail<T>`, `ErrorFromResult`, and `ErrorFromResult<T>` factories. `Result<T>` also has an implicit conversion from `T` to a successful typed result. No implicit conversion exists for `OperationError`.

## Error Model

`OperationError` exposes immutable `Message`, `Type`, and optional `Code` properties. The public constructor permits direct creation, and named factories provide the established categories. `Code` remains available for a future stable, machine-readable contract, but no `ErrorCodes` catalog is added until a concrete feature has a programmatic error condition.

`ErrorType` defines these ten categories:

- `NotFound`
- `Validation`
- `MalformedInput`
- `UnprocessableInput`
- `Conflict`
- `InvalidOperation`
- `Unauthorized`
- `Timeout`
- `ExternalDependencyFailure`
- `InternalError`

The categories deliberately use transport-neutral language. A future protocol, HTTP API, UI, or command-line adapter maps the category to its own native outcome and preserves a supplied code unchanged.

## Failure Propagation

Callers inspect `IsFailure` after each operation and either return the source result directly when the return type is compatible or call `ErrorFromResult` to change the payload type. Forwarding creates a new error list, reuses the original `OperationError` instances, and preserves their order, message, type, and optional code.

Forwarding deliberately discards source data and success message, performs no logging or translation, and does not guard a null source result. Passing a successful source result produces a successful destination with no errors. `Fail` guards its supplied `OperationError` so invalid factory use cannot create an error list containing a null entry.

## Exclusions

The slice intentionally excludes error-code constants, aggregation factories, `Map`, `Bind`, `Match`, result-specific extensions, logging, exception translation, localization, and presentation logic. Known failures are represented as Results; unexpected exceptions and cancellation remain separate exception-based channels.

## Validation

At the user's direction, this change does not add automated tests. Verification consists of restoring packages as needed and building the Core project and solution after implementation.
