using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Engine.Repositories.Models;

namespace ChangeLens.Engine.Repositories.Handlers;

/// <summary>
///     Handles the payload-free action that lists recent repository metadata.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. Recent entries are listed without revalidating them, and
///     supplied parameters are ignored exactly as before.
/// </remarks>
/// <param name="repositoryHistoryService">The repository-history capability. Cannot be <see langword="null" />.</param>
internal sealed class RepositoryListRecentHandler(IRepositoryHistoryService repositoryHistoryService) : IActionHandler
{
    /// <inheritdoc />
    public string Action => RepositoryActionConstants.ListRecentAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        var listResult = await repositoryHistoryService.ListRecentAsync(cancellationToken);
        return listResult.IsFailure
            ? ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<RepositoryListRecentResult>(listResult))
            : ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(RepositoryListRecentResult.FromSnapshot(listResult.Data!)));
    }
}
