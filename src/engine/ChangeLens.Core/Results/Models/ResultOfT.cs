namespace ChangeLens.Core.Results.Models;

/// <summary>
///     Represents the outcome of an operation with a typed success payload.
/// </summary>
/// <typeparam name="T">The type of the success payload.</typeparam>
public class Result<T> : Result
{
    /// <summary>
    ///     Gets the payload returned by a successful operation.
    /// </summary>
    /// <value>
    ///     The supplied payload on success, which can be <see langword="null" /> or a default value; otherwise, <see langword="default" />.
    /// </value>
    public T? Data { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="Result{T}" /> class.
    /// </summary>
    /// <param name="data">The success payload, or <see langword="default" /> for a failed result.</param>
    /// <param name="successMessage">The success message, or <see langword="null" /> when none applies.</param>
    protected internal Result(T? data, string? successMessage = null)
        : base(successMessage)
    {
        Data = data;
    }

    /// <summary>
    ///     Implicitly converts a value into a successful <see cref="Result{T}" />.
    /// </summary>
    /// <param name="value">The value to use as the successful payload.</param>
    /// <returns>A successful result containing <paramref name="value" />.</returns>
    public static implicit operator Result<T>(T value) => new(value);
}
