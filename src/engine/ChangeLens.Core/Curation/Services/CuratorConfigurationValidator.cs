using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.EvidenceBinder.Constants;
using ChangeLens.Core.EvidenceBinder.Models;

namespace ChangeLens.Core.Curation.Services;

/// <summary>
///     Validates the configured curator call limits against the binder's reserved budget.
/// </summary>
public static class CuratorConfigurationValidator
{
    private const double CharactersPerToken = 3.25;

    /// <summary>
    ///     Validates output and prompt reserves before services are composed.
    /// </summary>
    /// <param name="binderOptions">The configured binder budget. Cannot be <see langword="null" />.</param>
    /// <param name="curatorOptions">The configured curator call limits. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The configured reserves cannot hold the curator call.</exception>
    public static void Validate(EvidenceBinderOptions binderOptions, CuratorOptions curatorOptions)
    {
        ArgumentNullException.ThrowIfNull(binderOptions);
        ArgumentNullException.ThrowIfNull(curatorOptions);

        if (curatorOptions.MaximumOutputCharacters <= 0)
        {
            throw new InvalidOperationException("Curator maximum output characters must be positive.");
        }

        if (curatorOptions.MaximumOutputTokens <= 0)
        {
            throw new InvalidOperationException("Curator maximum output tokens must be positive.");
        }

        if (binderOptions.CuratorOutputCharacters < curatorOptions.MaximumOutputCharacters)
        {
            throw new InvalidOperationException(
                "Evidence binder curator output reserve must be at least the curator maximum output characters.");
        }

        var tokenReserve = checked((int)Math.Ceiling(curatorOptions.MaximumOutputTokens * CharactersPerToken));
        if (binderOptions.CuratorOutputCharacters < tokenReserve)
        {
            throw new InvalidOperationException(
                "Evidence binder curator output reserve must cover the configured maximum output-token estimate.");
        }

        var configuredContract = new BinderContract(
            CuratorContractConstants.RelationshipKinds,
            CuratorContractConstants.TrackShapes,
            new CuratorLimits(
                binderOptions.MaximumTracks,
                binderOptions.MaximumParticipantsPerTrack,
                binderOptions.MaximumRelationshipsPerTrack,
                binderOptions.MaximumItemsPerTrack,
                binderOptions.MaximumStatementCharacters,
                CuratorContractConstants.IdFormat));
        var renderedPromptCharacters = CuratorSystemMessage.Render(configuredContract).Length;
        if (renderedPromptCharacters > binderOptions.PromptReserveCharacters)
        {
            throw new InvalidOperationException(
                $"Evidence binder prompt reserve of {binderOptions.PromptReserveCharacters} characters is smaller than the "
                + $"rendered curator prompt of {renderedPromptCharacters} characters.");
        }
    }
}
