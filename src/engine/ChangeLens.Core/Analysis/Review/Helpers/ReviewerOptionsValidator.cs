using ChangeLens.Core.Analysis.EvidenceBinder.Helpers;
using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.Review.Models;

namespace ChangeLens.Core.Analysis.Review.Helpers;

/// <summary>
///     Validates the configured reviewer call limits against the binder's reserved budget.
/// </summary>
/// <remarks>
///     The reviewer reads the same binder as the curator, so the binder's prompt and output reserves must also hold the
///     reviewer's rendered system message and its maximum reply.
/// </remarks>
public static class ReviewerOptionsValidator
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

        var renderedPromptCharacters = ReviewerSystemMessage.Render(
            ModelBudgetReserveValidator.CreateConfiguredContract(binderOptions)).Length;
        ModelBudgetReserveValidator.ValidateReserves(
            binderOptions,
            reviewerOptions.MaximumOutputCharacters,
            reviewerOptions.MaximumOutputTokens,
            renderedPromptCharacters,
            "reviewer",
            "Reviewer",
            "the reviewer maximum output-token estimate");
    }
}
