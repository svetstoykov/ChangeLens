using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Infrastructure.IntegrationTests.Analysis.Snapshots.Support;

/// <summary>
///     Fails every composition with a fixed error, for composer failures real Git output cannot produce.
/// </summary>
internal sealed class FailingComparisonFileSummaryComposer : IComparisonFileSummaryComposer
{
    /// <summary>
    ///     The error code every composition fails with.
    /// </summary>
    internal const string ErrorCode = "fixture.composeFailed";

    /// <inheritdoc />
    public Result<ComparisonFileSummary> Compose(IReadOnlyList<ComparisonFileRecord> records) =>
        Result.Fail<ComparisonFileSummary>(OperationError.UnprocessableInput("The fixture composer always fails.", ErrorCode));
}
