namespace ChangeLens.Core.Results.Models;

/// <summary>
///     Defines the broad, transport-neutral categories of an operation error.
/// </summary>
public enum ErrorType
{
    /// <summary>
    ///     A requested resource is unavailable.
    /// </summary>
    NotFound,

    /// <summary>
    ///     One or more values violate validation rules.
    /// </summary>
    Validation,

    /// <summary>
    ///     Input is syntactically malformed.
    /// </summary>
    MalformedInput,

    /// <summary>
    ///     Input is valid in shape but cannot be processed semantically.
    /// </summary>
    UnprocessableInput,

    /// <summary>
    ///     An operation conflicts with the current state.
    /// </summary>
    Conflict,

    /// <summary>
    ///     An operation is invalid for the current application state.
    /// </summary>
    InvalidOperation,

    /// <summary>
    ///     The caller lacks permission to perform the operation.
    /// </summary>
    Unauthorized,

    /// <summary>
    ///     An operation did not complete within its allowed time.
    /// </summary>
    Timeout,

    /// <summary>
    ///     A required external dependency failed or was unavailable.
    /// </summary>
    ExternalDependencyFailure,

    /// <summary>
    ///     An unexpected internal condition prevented completion.
    /// </summary>
    InternalError,
}
