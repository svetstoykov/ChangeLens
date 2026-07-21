using System.Collections.ObjectModel;

namespace ChangeLens.Core.Results.Models;

/// <summary>
///     Represents the outcome of an operation without a success payload.
/// </summary>
public class Result
{
    private readonly List<OperationError> _errors = new();
    private readonly ReadOnlyCollection<OperationError> _errorsView;

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result" /> class.
    /// </summary>
    /// <param name="successMessage">The success message, or <see langword="null" /> when none applies.</param>
    protected Result(string? successMessage = null)
    {
        _errorsView = _errors.AsReadOnly();
        SuccessMessage = successMessage;
    }

    /// <summary>
    ///     Gets a value indicating whether the operation completed successfully.
    /// </summary>
    public bool IsSuccess => _errors.Count == 0;

    /// <summary>
    ///     Gets a value indicating whether the operation failed.
    /// </summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>
    ///     Gets the errors produced by the failed operation.
    /// </summary>
    /// <value>
    ///     The operation errors in their original order, or an empty collection for a successful result.
    /// </value>
    public IReadOnlyList<OperationError> Errors => _errorsView;

    /// <summary>
    ///     Gets the message associated with a successful operation.
    /// </summary>
    /// <value>
    ///     The success message, or <see langword="null" /> when none applies.
    /// </value>
    public string? SuccessMessage { get; protected set; }

    /// <summary>
    ///     Creates a successful result.
    /// </summary>
    /// <param name="message">The success message, or <see langword="null" /> when none applies.</param>
    /// <returns>A successful result with no errors.</returns>
    public static Result Success(string? message = null) => new(message);

    /// <summary>
    ///     Creates a successful result with a typed payload.
    /// </summary>
    /// <typeparam name="T">The type of the success payload.</typeparam>
    /// <param name="value">The payload returned by the successful operation.</param>
    /// <param name="message">The success message, or <see langword="null" /> when none applies.</param>
    /// <returns>A successful result containing <paramref name="value" />.</returns>
    public static Result<T> Success<T>(T value, string? message = null) =>
        new(value, message);

    /// <summary>
    ///     Creates a failed result with one error.
    /// </summary>
    /// <param name="error">The operation error. Cannot be <see langword="null" />.</param>
    /// <returns>A failed result containing <paramref name="error" />.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="error" /> is <see langword="null" />.
    /// </exception>
    public static Result Fail(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = new Result();
        result.AddError(error);
        return result;
    }

    /// <summary>
    ///     Creates a failed result with one error and a typed payload.
    /// </summary>
    /// <typeparam name="T">The type of the success payload.</typeparam>
    /// <param name="error">The operation error. Cannot be <see langword="null" />.</param>
    /// <returns>A failed result containing <paramref name="error" />.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="error" /> is <see langword="null" />.
    /// </exception>
    public static Result<T> Fail<T>(OperationError error)
    {
        ArgumentNullException.ThrowIfNull(error);

        var result = new Result<T>(default);
        result.AddError(error);
        return result;
    }

    /// <summary>
    ///     Creates a payload-free result that forwards errors from another result.
    /// </summary>
    /// <param name="input">The result whose errors are forwarded. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the errors from <paramref name="input" /> in their original order.</returns>
    /// <exception cref="NullReferenceException">
    ///     <paramref name="input" /> is <see langword="null" />.
    /// </exception>
    public static Result ErrorFromResult(Result input)
    {
        var result = new Result();

        foreach (var error in input.Errors)
        {
            result.AddError(error);
        }

        return result;
    }

    /// <summary>
    ///     Creates a typed result that forwards errors from another result.
    /// </summary>
    /// <typeparam name="T">The type of the success payload.</typeparam>
    /// <param name="input">The result whose errors are forwarded. Cannot be <see langword="null" />.</param>
    /// <returns>A typed result containing the errors from <paramref name="input" /> in their original order.</returns>
    /// <exception cref="NullReferenceException">
    ///     <paramref name="input" /> is <see langword="null" />.
    /// </exception>
    public static Result<T> ErrorFromResult<T>(Result input)
    {
        var result = new Result<T>(default);

        foreach (var error in input.Errors)
        {
            result.AddError(error);
        }

        return result;
    }

    private void AddError(OperationError error) => _errors.Add(error);
}
