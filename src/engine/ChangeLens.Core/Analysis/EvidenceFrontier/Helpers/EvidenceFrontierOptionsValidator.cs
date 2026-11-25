using ChangeLens.Core.Analysis.EvidenceFrontier.Models;

namespace ChangeLens.Core.Analysis.EvidenceFrontier.Helpers;

/// <summary>
///     Validates configured bounds for evidence frontier publication.
/// </summary>
public static class EvidenceFrontierOptionsValidator
{
    /// <summary>
    ///     Validates that the configured frontier entry cap is positive.
    /// </summary>
    /// <param name="options">The evidence frontier options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The maximum entry count is not positive.</exception>
    public static void Validate(EvidenceFrontierOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.MaximumEntries <= 0)
        {
            throw new InvalidOperationException(
                $"Publication configuration requires a positive evidence frontier maximum entry count; received {options.MaximumEntries}.");
        }
    }
}
