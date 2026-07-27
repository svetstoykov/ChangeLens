using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Comparisons.Services;

/// <summary>
///     Shapes comparison targets within the complete protocol response ceiling.
/// </summary>
/// <remarks>
///     <para>
///         The host registers this stateless service as a singleton. It is safe to use concurrently.
///     </para>
///     <para>
///         Descriptor support is classified from the complete unpaged set with stable correlation overhead.
///         Every returned page is then measured with its actual request identifier.
///     </para>
/// </remarks>
/// <param name="protocolSerializer">The production protocol serializer. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for target-page bounding outcomes. Cannot be <see langword="null" />.</param>
/// <exception cref="ArgumentNullException">
///     <paramref name="protocolSerializer" /> or <paramref name="logger" /> is <see langword="null" />.
/// </exception>
internal sealed class ComparisonTargetPageBuilder(
    IEngineProtocolSerializer protocolSerializer,
    ILogger<ComparisonTargetPageBuilder> logger) : IComparisonTargetPageBuilder
{
    /// <summary>
    ///     Provides stable correlation overhead when classifying serializer-unsupported descriptors.
    /// </summary>
    private const string DescriptorMeasurementRequestId = "comparison-target-page";

    private readonly IEngineProtocolSerializer _protocolSerializer =
        protocolSerializer ?? throw new ArgumentNullException(nameof(protocolSerializer));

    private readonly ILogger<ComparisonTargetPageBuilder> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));

    /// <inheritdoc />
    public Result<ComparisonTargetPageResult> Build(
        string requestId,
        ComparisonTargetSet targetSet)
    {
        ArgumentNullException.ThrowIfNull(requestId);
        ArgumentNullException.ThrowIfNull(targetSet);

        var mappedUnpagedTargets = targetSet.UnpagedTargets
            .Select(ComparisonTargetResult.FromDescriptor)
            .ToArray();
        var serializerUnsupportedFullNames = new HashSet<string>(StringComparer.Ordinal);
        var unsupportedTargetCount = targetSet.UnsupportedTargetCount;
        var previousUnsupportedTargetCount = -1;

        while (previousUnsupportedTargetCount != unsupportedTargetCount)
        {
            previousUnsupportedTargetCount = unsupportedTargetCount;
            var hasSupportedCandidateAfter = false;

            for (var index = mappedUnpagedTargets.Length - 1; index >= 0; index--)
            {
                var candidate = mappedUnpagedTargets[index];
                if (serializerUnsupportedFullNames.Contains(candidate.FullName))
                {
                    continue;
                }

                var measurement = this.Measure(
                    DescriptorMeasurementRequestId,
                    [candidate],
                    suggestedTarget: null,
                    hasSupportedCandidateAfter ? candidate.FullName : null,
                    targetSet.TargetSetToken,
                    unsupportedTargetCount);
                if (measurement.IsFailure)
                {
                    return Result.ErrorFromResult<ComparisonTargetPageResult>(measurement);
                }

                if (measurement.Data > ComparisonActionConstants.TargetPageBudgetBytes)
                {
                    serializerUnsupportedFullNames.Add(candidate.FullName);
                    unsupportedTargetCount++;
                    this._logger.LogDebug(
                        "Excluded comparison target {FullName} from the page because its serialized size " +
                        "{SerializedBytes} exceeds the page budget {BudgetBytes}.",
                        candidate.FullName,
                        measurement.Data,
                        ComparisonActionConstants.TargetPageBudgetBytes);
                    continue;
                }

                hasSupportedCandidateAfter = true;
            }
        }

        var supported = targetSet.Targets
            .Where(target => !serializerUnsupportedFullNames.Contains(target.FullName))
            .Select(ComparisonTargetResult.FromDescriptor)
            .ToArray();
        var suggestedTarget = targetSet.SuggestedTarget is null
            ? null
            : mappedUnpagedTargets.SingleOrDefault(
                candidate => candidate.FullName == targetSet.SuggestedTarget.FullName);
        if (suggestedTarget is not null &&
            serializerUnsupportedFullNames.Contains(suggestedTarget.FullName))
        {
            suggestedTarget = null;
        }

        var emitted = new List<ComparisonTargetResult>();
        string? nextCursor = null;

        for (var index = 0; index < supported.Length; index++)
        {
            var candidate = supported[index];
            var candidateCursor = index < supported.Length - 1
                ? candidate.FullName
                : null;
            var tentative = emitted.Append(candidate).ToArray();
            var measurement = this.Measure(
                requestId,
                tentative,
                suggestedTarget,
                candidateCursor,
                targetSet.TargetSetToken,
                unsupportedTargetCount);
            if (measurement.IsFailure)
            {
                return Result.ErrorFromResult<ComparisonTargetPageResult>(measurement);
            }

            if (measurement.Data <= ComparisonActionConstants.TargetPageBudgetBytes)
            {
                emitted.Add(candidate);
                nextCursor = candidateCursor;
                continue;
            }

            if (emitted.Count > 0)
            {
                nextCursor = emitted[^1].FullName;
                break;
            }

            if (suggestedTarget is not null)
            {
                suggestedTarget = null;
                measurement = this.Measure(
                    requestId,
                    [candidate],
                    suggestedTarget,
                    candidateCursor,
                    targetSet.TargetSetToken,
                    unsupportedTargetCount);
                if (measurement.IsFailure)
                {
                    return Result.ErrorFromResult<ComparisonTargetPageResult>(measurement);
                }

                if (measurement.Data <= ComparisonActionConstants.TargetPageBudgetBytes)
                {
                    emitted.Add(candidate);
                    nextCursor = candidateCursor;
                    continue;
                }
            }

            return this.CreateTooLargeFailure();
        }

        if (emitted.Count == supported.Length)
        {
            nextCursor = null;
        }

        var page = new ComparisonTargetPageResult(
            emitted,
            suggestedTarget,
            nextCursor,
            targetSet.TargetSetToken,
            unsupportedTargetCount);
        var finalMeasurement = this.Measure(
            requestId,
            page.Targets,
            page.SuggestedTarget,
            page.NextCursor,
            page.TargetSetToken,
            page.UnsupportedTargetCount);
        if (finalMeasurement.IsFailure)
        {
            return Result.ErrorFromResult<ComparisonTargetPageResult>(finalMeasurement);
        }

        return finalMeasurement.Data <= ComparisonActionConstants.TargetPageBudgetBytes
            ? Result.Success(page)
            : this.CreateTooLargeFailure();
    }

    private Result<ComparisonTargetPageResult> CreateTooLargeFailure()
    {
        this._logger.LogWarning(
            "Comparison target page build failed: even a single target exceeds the page budget " +
            "{BudgetBytes} bytes.",
            ComparisonActionConstants.TargetPageBudgetBytes);
        return Result.Fail<ComparisonTargetPageResult>(
            OperationError.UnprocessableInput(
                "The comparison exceeds the supported local inspection limit.",
                ComparisonErrorCode.TooLarge));
    }

    /// <summary>
    ///     Measures one tentative complete correlated target-page response.
    /// </summary>
    /// <param name="requestId">The request identifier.</param>
    /// <param name="targets">The tentative emitted targets.</param>
    /// <param name="suggestedTarget">The optional suggested target.</param>
    /// <param name="nextCursor">The optional continuation cursor.</param>
    /// <param name="targetSetToken">The target-set token.</param>
    /// <param name="unsupportedTargetCount">The current unsupported-target count.</param>
    /// <returns>The exact UTF-8 byte count or a serialization failure.</returns>
    private Result<int> Measure(
        string requestId,
        IReadOnlyList<ComparisonTargetResult> targets,
        ComparisonTargetResult? suggestedTarget,
        string? nextCursor,
        string targetSetToken,
        int unsupportedTargetCount)
    {
        var page = new ComparisonTargetPageResult(
            targets,
            suggestedTarget,
            nextCursor,
            targetSetToken,
            unsupportedTargetCount);
        return this._protocolSerializer.GetSerializedUtf8ByteCount(
            ProtocolResponseFactory.CreateWithValue(requestId, page));
    }
}
