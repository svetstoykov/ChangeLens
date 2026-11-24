using ChangeLens.Core.Analysis.ChangeAnatomy.Models;

namespace ChangeLens.Core.Analysis.ChangeAnatomy.Helpers;

/// <summary>
///     Validates configured bounds for deterministic change anatomy extraction.
/// </summary>
public static class ChangeAnatomyOptionsValidator
{
    /// <summary>
    ///     Validates that change anatomy extraction bounds are positive.
    /// </summary>
    /// <param name="options">The change anatomy options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Any required bound is not positive.</exception>
    public static void Validate(ChangeAnatomyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MinimumKeyLength <= 0 || options.MaximumKeysPerFile <= 0 || options.MaximumOccurrencesPerKey <= 0)
        {
            throw new InvalidOperationException("Change anatomy options must contain positive bounds.");
        }
    }
}
