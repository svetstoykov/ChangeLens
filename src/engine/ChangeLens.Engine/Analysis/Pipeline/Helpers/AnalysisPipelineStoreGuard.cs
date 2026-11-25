using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.Analysis.Pipeline.Helpers;

/// <summary>
///     Provides store-result guards that convert unexpected durable persistence failures into <see cref="InvalidOperationException" />.
/// </summary>
internal static class AnalysisPipelineStoreGuard
{
    /// <summary>Throws <see cref="InvalidOperationException" /> when <paramref name="result" /> indicates a failure.</summary>
    /// <param name="result">The durable store result to inspect. Cannot be <see langword="null" />.</param>
    /// <param name="operation">The failing operation phrase included in the exception message. Cannot be <see langword="null" />.</param>
    /// <exception cref="InvalidOperationException"><paramref name="result" /> failed.</exception>
    internal static void EnsureSuccess(Result result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"The analysis pipeline could not {operation}. Errors: {FormatErrorCodes(result.Errors)}.");
        }
    }

    /// <summary>Returns the payload of <paramref name="result" /> or throws <see cref="InvalidOperationException" /> on failure.</summary>
    /// <typeparam name="T">The store payload type.</typeparam>
    /// <param name="result">The durable store result to inspect. Cannot be <see langword="null" />.</param>
    /// <param name="operation">The failing operation phrase included in the exception message. Cannot be <see langword="null" />.</param>
    /// <returns>The non-null payload of <paramref name="result" />.</returns>
    /// <exception cref="InvalidOperationException"><paramref name="result" /> failed.</exception>
    internal static T EnsureSuccess<T>(Result<T> result, string operation)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                $"The analysis pipeline could not {operation}. Errors: {FormatErrorCodes(result.Errors)}.");
        }

        return result.Data!;
    }

    private static string FormatErrorCodes(IReadOnlyList<OperationError> errors) =>
        string.Join(", ", errors.Select(error => error.Code));
}
