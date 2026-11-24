using ChangeLens.Core.Analysis.ClaimChecking.Models;

namespace ChangeLens.Core.Analysis.ClaimChecking.Helpers;

/// <summary>
///     Validates claim-checking configuration bounds.
/// </summary>
public static class ClaimCheckingOptionsValidator
{
    /// <summary>
    ///     Validates enabled claim-checking limits before services are composed.
    /// </summary>
    /// <param name="options">The claim-checking options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Claim checking is enabled with a non-positive claim count, payload cap, attempt count, or output-token cap.
    /// </exception>
    public static void Validate(ClaimCheckingOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.Enabled
            && (options.MaximumClaims <= 0
                || options.MaximumPayloadCharacters <= 0
                || options.Attempts <= 0
                || options.MaximumOutputTokens <= 0))
        {
            throw new InvalidOperationException(
                "Publication configuration enables claim checking, but the checker claim count, payload characters, attempts, and "
                + "output tokens must all be positive.");
        }
    }
}
