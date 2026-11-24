using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.Shared.Constants;

namespace ChangeLens.Core.Analysis.EvidenceBinder.Helpers;

/// <summary>
///     Validates model output and prompt reserves against the evidence binder's configured budget.
/// </summary>
public static class ModelBudgetReserveValidator
{
    /// <summary>
    ///     Validates that configured model output limits and rendered prompt length fit inside the binder's reserves.
    /// </summary>
    /// <param name="binderOptions">The configured binder budget. Cannot be <see langword="null" />.</param>
    /// <param name="maximumOutputCharacters">The model call's maximum output character limit.</param>
    /// <param name="maximumOutputTokens">The model call's maximum output token limit.</param>
    /// <param name="renderedPromptCharacters">The length of the rendered system prompt in characters.</param>
    /// <param name="roleLower">The lowercase role name used in error messages. Cannot be <see langword="null" />.</param>
    /// <param name="roleTitle">The capitalized role name used in error messages. Cannot be <see langword="null" />.</param>
    /// <param name="tokenEstimatePhrase">The phrase describing the token limit in error messages. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">A required reference argument is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The configured reserves cannot hold the model call.</exception>
    public static void ValidateReserves(
        EvidenceBinderOptions binderOptions,
        int maximumOutputCharacters,
        int maximumOutputTokens,
        int renderedPromptCharacters,
        string roleLower,
        string roleTitle,
        string tokenEstimatePhrase)
    {
        ArgumentNullException.ThrowIfNull(binderOptions);
        ArgumentNullException.ThrowIfNull(roleLower);
        ArgumentNullException.ThrowIfNull(roleTitle);
        ArgumentNullException.ThrowIfNull(tokenEstimatePhrase);

        if (maximumOutputCharacters <= 0)
        {
            throw new InvalidOperationException($"{roleTitle} maximum output characters must be positive.");
        }

        if (maximumOutputTokens <= 0)
        {
            throw new InvalidOperationException($"{roleTitle} maximum output tokens must be positive.");
        }

        if (binderOptions.CuratorOutputCharacters < maximumOutputCharacters)
        {
            throw new InvalidOperationException(
                $"Evidence binder curator output reserve must be at least the {roleLower} maximum output characters.");
        }

        var tokenReserve = checked((int)Math.Ceiling(maximumOutputTokens * ModelBudgetConstants.CharactersPerToken));
        if (binderOptions.CuratorOutputCharacters < tokenReserve)
        {
            throw new InvalidOperationException(
                $"Evidence binder curator output reserve must cover {tokenEstimatePhrase}.");
        }

        if (renderedPromptCharacters > binderOptions.PromptReserveCharacters)
        {
            throw new InvalidOperationException(
                $"Evidence binder prompt reserve of {binderOptions.PromptReserveCharacters} characters is smaller than the "
                + $"rendered {roleLower} prompt of {renderedPromptCharacters} characters.");
        }
    }

    /// <summary>
    ///     Builds the binder contract the configured options produce, so prompts can be rendered before a binder exists.
    /// </summary>
    /// <param name="binderOptions">The configured binder budget. Cannot be <see langword="null" />.</param>
    /// <returns>The contract with the configured curator limits.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="binderOptions" /> is <see langword="null" />.</exception>
    public static BinderContract CreateConfiguredContract(EvidenceBinderOptions binderOptions)
    {
        ArgumentNullException.ThrowIfNull(binderOptions);
        return new(
            CuratorContractConstants.RelationshipKinds,
            CuratorContractConstants.TrackShapes,
            new CuratorLimits(
                binderOptions.MaximumTracks,
                binderOptions.MaximumParticipantsPerTrack,
                binderOptions.MaximumRelationshipsPerTrack,
                binderOptions.MaximumItemsPerTrack,
                binderOptions.MaximumStatementCharacters,
                CuratorContractConstants.IdFormat));
    }
}
