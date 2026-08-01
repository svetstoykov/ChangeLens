using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Engine.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Handlers;

/// <summary>
///     Handles the action that removes one recent repository-history entry.
/// </summary>
/// <remarks>
///     The host registers this handler as scoped. The identifier must be a canonical GUID in <c>D</c> format; a
///     non-canonical spelling is rejected as an invalid request.
/// </remarks>
/// <param name="busyGuard">The repository-busy guard. Cannot be <see langword="null" />.</param>
/// <param name="repositoryHistoryService">The repository-history capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class RepositoryRemoveRecentHandler(
    IRepositoryBusyGuard busyGuard,
    IRepositoryHistoryService repositoryHistoryService,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => RepositoryActionConstants.RemoveRecentAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<RepositoryRemoveRecentParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var busyResult = await busyGuard.CheckRepositoryIdAsync(parametersResult.Data!.RepositoryId, cancellationToken);
        if (busyResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, busyResult.Errors);
        }

        if (!Guid.TryParseExact(parametersResult.Data!.RepositoryId, "D", out var repositoryId) ||
            !string.Equals(parametersResult.Data.RepositoryId, repositoryId.ToString("D"), StringComparison.Ordinal))
        {
            return ProtocolResponseFactory.FromError(request.RequestId,
                OperationError.Validation("The repositories.removeRecent repositoryId must be a canonical GUID in 'D' format.",
                    EngineErrorCode.InvalidRequest));
        }

        var result = await repositoryHistoryService.RemoveRecentAsync(repositoryId, cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }
}
