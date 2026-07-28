using System.Text.Json;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Handlers;

/// <summary>
///     Handles the action that removes one recent repository-history entry.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. The identifier must be a canonical GUID in <c>D</c> format;
///     a non-canonical spelling is rejected as an invalid request.
/// </remarks>
/// <param name="repositoryHistoryService">The repository-history capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class RepositoryRemoveRecentHandler(
    IRepositoryHistoryService repositoryHistoryService,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public string Action => RepositoryActionConstants.RemoveRecentAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, this.Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<RepositoryRemoveRecentParameters>(request.Parameters, this.Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        if (!Guid.TryParseExact(parametersResult.Data!.RepositoryId, "D", out var repositoryId) ||
            !string.Equals(parametersResult.Data.RepositoryId, repositoryId.ToString("D"), StringComparison.Ordinal))
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.Validation(
                    "The repositories.removeRecent repositoryId must be a canonical GUID in 'D' format.",
                    EngineErrorCode.InvalidRequest));
        }

        var result = await repositoryHistoryService.RemoveRecentAsync(repositoryId, cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }
}
