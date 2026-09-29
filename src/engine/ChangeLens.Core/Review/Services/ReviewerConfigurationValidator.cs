using ChangeLens.Core.Curation.Services;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Review.Models;

namespace ChangeLens.Core.Review.Services;

/// <summary>
///     Validates the configured reviewer call limits against the binder's reserved budget.
/// </summary>
/// <remarks>
///     The reviewer reads the same binder as the curator, so the binder's prompt and output reserves must also hold the
///     reviewer's rendered system message and its maximum reply.
/// </remarks>
public static class ReviewerConfigurationValidator
{
    /// <summary>
    ///     Validates output and prompt reserves before services are composed. A disabled reviewer is not validated.
    /// </summary>
    /// <param name="binderOptions">The configured binder budget. Cannot be <see langword="null" />.</param>
    /// <param name="reviewerOptions">The configured reviewer call limits. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">The configured reserves cannot hold the reviewer call.</exception>
    public static void Validate(EvidenceBinderOptions binderOptions, ReviewerOptions reviewerOptions)
    {
        ArgumentNullException.ThrowIfNull(binderOptions);
        ArgumentNullException.ThrowIfNull(reviewerOptions);

        if (!reviewerOptions.Enabled)
        {
            return;
        }

        if (reviewerOptions.MaximumOutputCharacters <= 0)
        {
            throw new InvalidOperationException("Reviewer maximum output characters must be positive.");
        }

        if (reviewerOptions.MaximumOutputTokens <= 0)
        {
            throw new InvalidOperationException("Reviewer maximum output tokens must be positive.");
        }

        if (binderOptions.CuratorOutputCharacters < reviewerOptions.MaximumOutputCharacters)
        {
            throw new InvalidOperationException(
                "Evidence binder curator output reserve must be at least the reviewer maximum output characters.");
        }

        var tokenReserve = checked((int)Math.Ceiling(reviewerOptions.MaximumOutputTokens * CuratorConfigurationValidator.CharactersPerToken));
        if (binderOptions.CuratorOutputCharacters < tokenReserve)
        {
            throw new InvalidOperationException(
                "Evidence binder curator output reserve must cover the reviewer maximum output-token estimate.");
        }

        var renderedPromptCharacters = ReviewerSystemMessage.Render(CuratorConfigurationValidator.CreateConfiguredContract(binderOptions)).Length;
        if (renderedPromptCharacters > binderOptions.PromptReserveCharacters)
        {
            throw new InvalidOperationException(
                $"Evidence binder prompt reserve of {binderOptions.PromptReserveCharacters} characters is smaller than the "
                + $"rendered reviewer prompt of {renderedPromptCharacters} characters.");
        }
    }
}
