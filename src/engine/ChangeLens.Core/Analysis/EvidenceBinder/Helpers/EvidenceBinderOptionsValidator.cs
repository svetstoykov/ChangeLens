using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.Shared.Constants;

namespace ChangeLens.Core.Analysis.EvidenceBinder.Helpers;

/// <summary>
///     Validates configured bounds and reserves for evidence binder assembly.
/// </summary>
public static class EvidenceBinderOptionsValidator
{
    /// <summary>
    ///     Validates evidence binder bounds and reserves, throwing when any invariant is violated.
    /// </summary>
    /// <param name="options">The evidence binder options to validate. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">Any limit or reserve violates its invariant.</exception>
    public static void Validate(EvidenceBinderOptions options)
    {
        if (!TryValidate(options, out var error))
        {
            throw new InvalidOperationException(error);
        }
    }

    /// <summary>
    ///     Attempts to validate evidence binder bounds and reserves without throwing on invalid limits.
    /// </summary>
    /// <param name="options">The evidence binder options to validate. Cannot be <see langword="null" />.</param>
    /// <param name="error">When this method returns <see langword="false" />, the validation message.</param>
    /// <returns><see langword="true" /> when all limits and reserves are valid; otherwise, <see langword="false" />.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="options" /> is <see langword="null" />.</exception>
    public static bool TryValidate(EvidenceBinderOptions options, out string? error)
    {
        ArgumentNullException.ThrowIfNull(options);

        var derivedMaximumBinderCharacters =
            Math.Floor(options.ContextWindowTokens * ModelBudgetConstants.CharactersPerToken)
            - options.CuratorOutputCharacters
            - options.PromptReserveCharacters;

        if (options.ContextWindowTokens <= 0
            || options.CuratorOutputCharacters < 0
            || options.PromptReserveCharacters < 0
            || options.MaximumBinderCharacters is <= 0
            || (options.MaximumBinderCharacters is null && derivedMaximumBinderCharacters <= 0)
            || options.EffectiveMaximumBinderCharacters <= 0
            || !double.IsFinite(options.TargetUtilization)
            || options.TargetUtilization is <= 0 or > 1
            || options.MaximumOrientationPathsPerDirectory <= 0
            || options.MaximumOrientationPaths <= 0
            || options.MaximumDeveloperContextCharacters <= 0
            || options.MaximumOmissionItems <= 0
            || options.MaximumOmissionItemCharacters <= 0
            || options.MaximumTracks <= 0
            || options.MaximumParticipantsPerTrack <= 0
            || options.MaximumRelationshipsPerTrack <= 0
            || options.MaximumItemsPerTrack <= 0
            || options.MaximumStatementCharacters <= 0)
        {
            error = "Every evidence binder limit must be positive, with non-negative reserves.";
            return false;
        }

        error = null;
        return true;
    }
}
