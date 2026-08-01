using System.Text.Json;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Engine.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Handlers;

/// <summary>
///     Handles the action that inspects and opens a selected repository.
/// </summary>
/// <remarks>
///     The host registers this handler as scoped. It serves one request and does not need to be thread-safe.
/// </remarks>
/// <param name="busyGuard">The repository-busy guard. Cannot be <see langword="null" />.</param>
/// <param name="repositoryHistoryService">The repository-history capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class RepositoryOpenHandler(
    IRepositoryBusyGuard busyGuard,
    IRepositoryHistoryService repositoryHistoryService,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => RepositoryActionConstants.OpenAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<RepositoryOpenParameters>(request.Parameters, Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var busyResult = await busyGuard.CheckPathAsync(parametersResult.Data!.Path, cancellationToken);
        if (busyResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, busyResult.Errors);
        }

        var openResult = await repositoryHistoryService.OpenAsync(parametersResult.Data!.Path, cancellationToken);
        if (openResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<RepositoryOpenResult>(openResult));
        }

        return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(RepositoryOpenResult.FromOpenedRepository(openResult.Data!)));
    }
}
