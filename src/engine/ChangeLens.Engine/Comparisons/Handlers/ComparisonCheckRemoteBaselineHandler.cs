using System.Text.Json;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Core.AnalysisRuns.Interfaces;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Comparisons.Handlers;

/// <summary>
///     Handles the action that checks a cached remote-tracking baseline against the server.
/// </summary>
/// <remarks>
///     The host registers this handler as scoped. A baseline result the protocol has not approved is returned as a
///     domain-coded internal error.
/// </remarks>
/// <param name="busyGuard">The repository-busy guard. Cannot be <see langword="null" />.</param>
/// <param name="remoteBaselineTracker">
///     The remote baseline detection and refresh capability. Cannot be <see langword="null" />.
/// </param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class ComparisonCheckRemoteBaselineHandler(
    IRepositoryBusyGuard busyGuard,
    IGitRemoteBaselineTracker remoteBaselineTracker,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => ComparisonActionConstants.CheckRemoteBaselineAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonCheckRemoteBaselineParameters>(request.Parameters, Action);
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
        var checkResult = await remoteBaselineTracker.CheckAsync(parameters.Path, parameters.Target, cancellationToken);
        if (checkResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(request.RequestId, Result.ErrorFromResult<RemoteBaselineResult>(checkResult));
        }

        var result = checkResult.Data! switch
        {
            { State: RemoteBaselineState.Current, RemoteRevision: { } revision } =>
                Result.Success<RemoteBaselineResult>(new CurrentRemoteBaselineResult(revision)),
            { State: RemoteBaselineState.Moved, RemoteRevision: { } revision } =>
                Result.Success<RemoteBaselineResult>(new MovedRemoteBaselineResult(revision)),
            { State: RemoteBaselineState.NoRemote } =>
                Result.Success<RemoteBaselineResult>(new NoRemoteRemoteBaselineResult()),
            _ => OperationError.InternalError(
                "The remote baseline check result is not approved for the engine protocol.",
                ComparisonErrorCode.UnmappedRemoteBaselineState),
        };
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }
}
