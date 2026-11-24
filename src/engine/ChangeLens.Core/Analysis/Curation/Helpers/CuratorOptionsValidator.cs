using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.Shared.Helpers;

namespace ChangeLens.Core.Analysis.Curation.Helpers;

/// <summary>
///     Validates the configured curator call limits against the binder's reserved budget.
/// </summary>
public static class CuratorOptionsValidator
{
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

        var renderedPromptCharacters = CuratorSystemMessage.Render(
            ModelBudgetReserveValidator.CreateConfiguredContract(binderOptions)).Length;
        ModelBudgetReserveValidator.ValidateReserves(
            binderOptions,
            curatorOptions.MaximumOutputCharacters,
            curatorOptions.MaximumOutputTokens,
            renderedPromptCharacters,
            "curator",
            "Curator",
            "the configured maximum output-token estimate");
    }
}
