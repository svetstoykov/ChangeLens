using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Handlers;

/// <summary>
///     Handles the payload-free action that restores the last selected repository.
/// </summary>
/// <remarks>
///     The host registers this handler as scoped. The action carries no payload, and supplied parameters are ignored.
/// </remarks>
/// <param name="repositoryHistoryService">The repository-history capability. Cannot be <see langword="null" />.</param>
internal sealed class RepositoryRestoreLastHandler(IRepositoryHistoryService repositoryHistoryService) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => RepositoryActionConstants.RestoreLastAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        var restorationResult = await repositoryHistoryService.RestoreLastAsync(cancellationToken);
        if (restorationResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<RepositoryRestoreResult>(restorationResult));
        }

        RepositoryRestoreResult result = restorationResult.Data!.HistoryEntry is null
            ? new NoRepositoryRestoreResult()
            : new RestoredRepositoryResult(
                restorationResult.Data.HistoryEntry.RepositoryId,
                RepositoryResult.FromDescriptor(restorationResult.Data.Repository!),
                restorationResult.Data.HistoryEntry.PreferredTargetFullName);
        return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(result));
    }
}
