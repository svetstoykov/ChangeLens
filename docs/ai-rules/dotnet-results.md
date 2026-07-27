# Validation and Result Architecture

Read before adding or changing validation, error handling, or any code that returns or forwards a `Result` / `Result<T>`.

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
- Keep the implementation deliberately small. Do not add `Bind`, `Map`, result builders, or a result-specific extension-method framework. `Result<T>` may define an implicit conversion from `OperationError` (mirroring its existing implicit conversion from `T`) so failure call sites do not have to restate the payload type; `Result` stays without one, since its non-generic `Fail` call sites carry no such repetition.
- Treat expected failures as Result data. Treat unexpected exceptions through the separate exception boundary.
- Keep cancellation exception-based and distinct from timeout or other Result failures.
- Put Result types, operation errors, error categories, and stable codes in Core.
- Design and test the concrete Result API before implementing it. Do not infer unapproved member-level details from this architectural direction.
