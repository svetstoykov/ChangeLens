using System.Text.Json;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Comparisons.Handlers;

/// <summary>
///     Handles the action that prepares comparison facts for a selected target.
/// </summary>
/// <remarks>
///     <para>
///         The host registers this handler as scoped.
///     </para>
///     <para>
///         The preferred target is saved only after preparation succeeds, and a failed save is returned in place of
///         the prepared result so the client never sees a result the engine did not retain.
///     </para>
/// </remarks>
/// <param name="busyGuard">The repository-busy guard. Cannot be <see langword="null" />.</param>
/// <param name="comparisonPreparer">The comparison-preparation capability. Cannot be <see langword="null" />.</param>
/// <param name="repositoryHistoryService">The repository-history capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class ComparisonPrepareHandler(
    IRepositoryBusyGuard busyGuard,
    IGitComparisonPreparer comparisonPreparer,
    IRepositoryHistoryService repositoryHistoryService,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => ComparisonActionConstants.PrepareAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonPrepareParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var busyResult = await busyGuard.CheckPathAsync(parametersResult.Data!.Path, cancellationToken);
        if (busyResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, busyResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var preparationResult = await comparisonPreparer.PrepareAsync(parameters.Path, parameters.Target, cancellationToken);
        if (preparationResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<ComparisonPrepareResult>(preparationResult));
        }

        var preferenceResult = await repositoryHistoryService.SavePreferredTargetAsync(preparationResult.Data!.Repository.CanonicalPath,
            preparationResult.Data.Target.FullName, cancellationToken);
        if (preferenceResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<ComparisonPrepareResult>(preferenceResult));
        }

        return ProtocolResponseFactory.FromResult(request.RequestId,
            Result.Success(ComparisonPrepareResult.FromPreparedComparison(preparationResult.Data!)));
    }
}
