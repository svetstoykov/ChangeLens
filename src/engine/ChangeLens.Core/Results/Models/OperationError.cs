namespace ChangeLens.Core.Results.Models;

/// <summary>
///     Represents structured detail about a known operation failure.
/// </summary>
public sealed class OperationError
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="OperationError" /> class.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="type">The broad, transport-neutral error category.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="message" /> is <see langword="null" />.
    /// </exception>
    public OperationError(string message, ErrorType type, string? code = null)
    {
        ArgumentNullException.ThrowIfNull(message);

        Message = message;
        Type = type;
        Code = code;
    }

    /// <summary>
    ///     Gets the human-readable failure message.
    /// </summary>
    public string Message { get; }

    /// <summary>
    ///     Gets the broad, transport-neutral error category.
    /// </summary>
    public ErrorType Type { get; }

    /// <summary>
    ///     Gets the stable machine-readable error code.
    /// </summary>
    /// <value>
    ///     The error code, or <see langword="null" /> when no code applies.
    /// </value>
    public string? Code { get; }

    /// <summary>
    ///     Creates an error for an unavailable resource.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.NotFound" />.</returns>
    public static OperationError NotFound(string message, string? code = null) =>
        new(message, ErrorType.NotFound, code);

    /// <summary>
    ///     Creates an error for values that violate validation rules.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.Validation" />.</returns>
    public static OperationError Validation(string message, string? code = null) =>
        new(message, ErrorType.Validation, code);

    /// <summary>
    ///     Creates an error for syntactically malformed input.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.MalformedInput" />.</returns>
    public static OperationError MalformedInput(string message, string? code = null) =>
        new(message, ErrorType.MalformedInput, code);

    /// <summary>
    ///     Creates an error for input that cannot be processed semantically.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.UnprocessableInput" />.</returns>
    public static OperationError UnprocessableInput(string message, string? code = null) =>
        new(message, ErrorType.UnprocessableInput, code);

    /// <summary>
    ///     Creates an error for an operation that conflicts with the current state.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.Conflict" />.</returns>
    public static OperationError Conflict(string message, string? code = null) =>
        new(message, ErrorType.Conflict, code);

    /// <summary>
    ///     Creates an error for an operation that is invalid for the current application state.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.InvalidOperation" />.</returns>
    public static OperationError InvalidOperation(string message, string? code = null) =>
        new(message, ErrorType.InvalidOperation, code);

    /// <summary>
    ///     Creates an error for a caller without permission to perform an operation.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.Unauthorized" />.</returns>
    public static OperationError Unauthorized(string message, string? code = null) =>
        new(message, ErrorType.Unauthorized, code);

    /// <summary>
    ///     Creates an error for an operation that exceeded its allowed time.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.Timeout" />.</returns>
    public static OperationError Timeout(string message, string? code = null) =>
        new(message, ErrorType.Timeout, code);

    /// <summary>
    ///     Creates an error for a failed or unavailable external dependency.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.ExternalDependencyFailure" />.</returns>
    public static OperationError ExternalDependencyFailure(string message, string? code = null) =>
        new(message, ErrorType.ExternalDependencyFailure, code);

    /// <summary>
    ///     Creates an error for an unexpected internal condition.
    /// </summary>
    /// <param name="message">The human-readable failure message. Cannot be <see langword="null" />.</param>
    /// <param name="code">The stable machine-readable error code, or <see langword="null" /> when none applies.</param>
    /// <returns>An error categorized as <see cref="ErrorType.InternalError" />.</returns>
    public static OperationError InternalError(string message, string? code = null) =>
        new(message, ErrorType.InternalError, code);
}
