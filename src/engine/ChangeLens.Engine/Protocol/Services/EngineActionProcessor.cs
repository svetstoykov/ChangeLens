using System.Diagnostics;
using System.Text.Json;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Core.Comparisons.Interfaces;
using ChangeLens.Core.Comparisons.Services;
using ChangeLens.Core.EngineStatus.Interfaces;
using ChangeLens.Core.Git.Services;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Comparisons.Interfaces;
using ChangeLens.Engine.Comparisons.Models;
using ChangeLens.Engine.Comparisons.Services;
using ChangeLens.Engine.EngineStatus.Constants;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Preferences.Constants;
using ChangeLens.Engine.Preferences.Interfaces;
using ChangeLens.Engine.Preferences.Models;
using ChangeLens.Engine.Preferences.Services;
using ChangeLens.Engine.Repositories.Constants;
using ChangeLens.Engine.Repositories.Interfaces;
using ChangeLens.Engine.Repositories.Models;
using ChangeLens.Engine.Repositories.Services;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Engine.Protocol.Services;

/// <summary>
///     Selects an approved engine action and maps its concrete Core Result to the protocol.
/// </summary>
/// <remarks>
///     The host registers this service as a singleton and processes actions sequentially. The service depends only on
///     singleton-safe collaborators and does not maintain mutable state.
/// </remarks>
/// <param name="engineStatusService">The engine-status capability. Cannot be <see langword="null" />.</param>
/// <param name="gitRepositoryInspector">The Git repository inspection capability. Cannot be <see langword="null" />.</param>
/// <param name="comparisonTargetDiscovery">
///     The comparison-target discovery capability. Cannot be <see langword="null" />.
/// </param>
/// <param name="comparisonPreparer">The comparison-preparation capability. Cannot be <see langword="null" />.</param>
/// <param name="comparisonFreshnessChecker">
///     The comparison freshness capability. Cannot be <see langword="null" />.
/// </param>
/// <param name="comparisonTargetPageBuilder">
///     The bounded target-page builder. Cannot be <see langword="null" />.
/// </param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for action outcomes. Cannot be <see langword="null" />.</param>
internal sealed class EngineActionProcessor(
    IEngineStatusService engineStatusService,
    IRepositoryHistoryService repositoryHistoryService,
    IColorThemePreferenceService colorThemePreferenceService,
    IGitComparisonTargetDiscovery comparisonTargetDiscovery,
    IGitComparisonPreparer comparisonPreparer,
    IGitComparisonFreshnessChecker comparisonFreshnessChecker,
    IComparisonTargetPageBuilder comparisonTargetPageBuilder,
    IEngineProtocolSerializer protocolSerializer,
    ILogger<EngineActionProcessor> logger) : IEngineActionProcessor
{
    /// <inheritdoc />
    public async Task<ProtocolResponse> ProcessAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var startedAt = Stopwatch.GetTimestamp();

        ProtocolResponse response;
        try
        {
            if (request.ProtocolVersion != EngineProtocolConstants.CurrentVersion)
            {
                response = ProtocolResponseFactory.FromError(
                    request.RequestId,
                    OperationError.UnprocessableInput(
                        $"Protocol version {request.ProtocolVersion} is not supported.",
                        EngineErrorCode.UnsupportedVersion));
            }
            else
            {
                response = await this.ProcessKnownVersionAsync(request, cancellationToken);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var loggedException = IsComparisonAction(request.Action)
                ? new InvalidOperationException("Unexpected comparison action failure.")
                : exception;
            logger.LogError(
                loggedException,
                "Unexpected failure processing engine action {RequestId} for {Action} with error {ErrorCode} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Action,
                EngineErrorCode.UnexpectedFailure,
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);

            return ProtocolResponseFactory.CreateUnexpectedFailure(request.RequestId);
        }

        this.LogOutcome(response, request, Stopwatch.GetElapsedTime(startedAt));
        return response;
    }

    /// <summary>
    ///     Selects one action from the current protocol version.
    /// </summary>
    /// <param name="request">The current-version request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the action.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the selected action response.
    /// </returns>
    private async Task<ProtocolResponse> ProcessKnownVersionAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Action != EngineStatusActionConstants.CheckStatusAction &&
            IsKnownAction(request.Action))
        {
            var readinessResult = await engineStatusService.CheckStatusAsync(cancellationToken);
            if (readinessResult.IsFailure)
            {
                return ProtocolResponseFactory.FromResult(request.RequestId, readinessResult);
            }
        }

        return request.Action switch
        {
            RepositoryActionConstants.OpenAction =>
                await this.ProcessRepositoryOpenAsync(request, cancellationToken),
            RepositoryActionConstants.RestoreLastAction =>
                await this.ProcessRepositoryRestoreLastAsync(request, cancellationToken),
            RepositoryActionConstants.ListRecentAction =>
                await this.ProcessRepositoryListRecentAsync(request, cancellationToken),
            RepositoryActionConstants.RemoveRecentAction =>
                await this.ProcessRepositoryRemoveRecentAsync(request, cancellationToken),
            ComparisonActionConstants.ListTargetsAction =>
                await this.ProcessComparisonListTargetsAsync(request, cancellationToken),
            ComparisonActionConstants.PrepareAction =>
                await this.ProcessComparisonPrepareAsync(request, cancellationToken),
            ComparisonActionConstants.CheckFreshnessAction =>
                await this.ProcessComparisonCheckFreshnessAsync(request, cancellationToken),
            EngineStatusActionConstants.CheckStatusAction =>
                await this.ProcessCheckStatusAsync(request, cancellationToken),
            PreferenceActionConstants.GetColorThemeAction =>
                await this.ProcessGetColorThemeAsync(request, cancellationToken),
            PreferenceActionConstants.SetColorThemeAction =>
                await this.ProcessSetColorThemeAsync(request, cancellationToken),
            _ =>
                ProtocolResponseFactory.FromError(
                    request.RequestId,
                    OperationError.NotFound(
                        $"The action '{request.Action}' is not recognized.",
                        EngineErrorCode.UnknownAction)),
        };
    }

    /// <summary>
    ///     Asynchronously binds and executes the repository-open action.
    /// </summary>
    /// <param name="request">The current-version repository-open request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for repository inspection.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the mapped repository response.
    /// </returns>
    private async Task<ProtocolResponse> ProcessRepositoryOpenAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.Validation(
                    "The repositories.open action requires parameters.",
                    EngineErrorCode.InvalidRequest));
        }

        var parametersResult = protocolSerializer.DeserializeParameters<RepositoryOpenParameters>(
            request.Parameters,
            RepositoryActionConstants.OpenAction);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var openResult = await repositoryHistoryService.OpenAsync(
            parametersResult.Data!.Path,
            cancellationToken);
        if (openResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<RepositoryOpenResult>(openResult));
        }

        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            Result.Success(RepositoryOpenResult.FromOpenedRepository(openResult.Data!)));
    }

    /// <summary>
    ///     Asynchronously binds and executes comparison-target discovery.
    /// </summary>
    /// <param name="request">The current-version target-list request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for discovery.
    /// </param>
    /// <returns>A task that represents the asynchronous operation and contains the bounded target response.</returns>
    private async Task<ProtocolResponse> ProcessComparisonListTargetsAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return MissingParameters(request.RequestId, ComparisonActionConstants.ListTargetsAction);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonListTargetsParameters>(
            request.Parameters,
            ComparisonActionConstants.ListTargetsAction);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var discoveryResult = await comparisonTargetDiscovery.ListAsync(
            parameters.Path,
            parameters.Query,
            parameters.After,
            parameters.TargetSetToken,
            cancellationToken);
        if (discoveryResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonTargetPageResult>(discoveryResult));
        }

        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            comparisonTargetPageBuilder.Build(request.RequestId, discoveryResult.Data!));
    }

    /// <summary>
    ///     Asynchronously binds and executes comparison preparation.
    /// </summary>
    /// <param name="request">The current-version preparation request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for preparation.
    /// </param>
    /// <returns>A task that represents the asynchronous operation and contains prepared comparison facts.</returns>
    private async Task<ProtocolResponse> ProcessComparisonPrepareAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return MissingParameters(request.RequestId, ComparisonActionConstants.PrepareAction);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ComparisonPrepareParameters>(
            request.Parameters,
            ComparisonActionConstants.PrepareAction);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var preparationResult = await comparisonPreparer.PrepareAsync(
            parameters.Path,
            parameters.Target,
            cancellationToken);
        if (preparationResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonPrepareResult>(preparationResult));
        }

        var preferenceResult = await repositoryHistoryService.SavePreferredTargetAsync(
            preparationResult.Data!.Repository.CanonicalPath,
            preparationResult.Data.Target.FullName,
            cancellationToken);
        if (preferenceResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonPrepareResult>(preferenceResult));
        }

        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            Result.Success(
                ComparisonPrepareResult.FromPreparedComparison(preparationResult.Data!)));
    }

    /// <summary>
    ///     Asynchronously binds and executes a prepared comparison freshness check.
    /// </summary>
    /// <param name="request">The current-version freshness request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the check.
    /// </param>
    /// <returns>A task that represents the asynchronous operation and contains the tagged freshness state.</returns>
    private async Task<ProtocolResponse> ProcessComparisonCheckFreshnessAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return MissingParameters(
                request.RequestId,
                ComparisonActionConstants.CheckFreshnessAction);
        }

        var parametersResult =
            protocolSerializer.DeserializeParameters<ComparisonCheckFreshnessParameters>(
                request.Parameters,
                ComparisonActionConstants.CheckFreshnessAction);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var parameters = parametersResult.Data!;
        var freshnessResult = await comparisonFreshnessChecker.CheckAsync(
            parameters.Path,
            parameters.Target,
            parameters.FreshnessToken,
            cancellationToken);
        if (freshnessResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ComparisonFreshnessResult>(freshnessResult));
        }

        ComparisonFreshnessResult result = freshnessResult.Data switch
        {
            ComparisonFreshnessState.Current => new CurrentComparisonFreshnessResult(),
            ComparisonFreshnessState.Stale => new StaleComparisonFreshnessResult(),
            _ => throw new InvalidOperationException(
                "The comparison freshness state is not approved for the engine protocol."),
        };
        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            Result.Success(result));
    }

    /// <summary>
    ///     Asynchronously executes the payload-free engine-status action.
    /// </summary>
    /// <param name="request">The validated status request. Cannot be <see langword="null" />.</param>
    /// <param name="cancellationToken">
    ///     A <see cref="CancellationToken" /> to observe while waiting for the status check.
    /// </param>
    /// <returns>
    ///     A task that represents the asynchronous operation. The task result contains the mapped status response.
    /// </returns>
    private async Task<ProtocolResponse> ProcessCheckStatusAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        var result = await engineStatusService.CheckStatusAsync(cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }

    private async Task<ProtocolResponse> ProcessRepositoryRestoreLastAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        var restorationResult = await repositoryHistoryService.RestoreLastAsync(cancellationToken);
        if (restorationResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<RepositoryRestoreResult>(restorationResult));
        }

        RepositoryRestoreResult result = restorationResult.Data!.HistoryEntry is null
            ? new NoRepositoryRestoreResult()
            : new RestoredRepositoryResult(
                restorationResult.Data.HistoryEntry.RepositoryId,
                RepositoryResult.FromDescriptor(restorationResult.Data.Repository!),
                restorationResult.Data.HistoryEntry.PreferredTargetFullName);
        return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(result));
    }

    private async Task<ProtocolResponse> ProcessRepositoryListRecentAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        var listResult = await repositoryHistoryService.ListRecentAsync(cancellationToken);
        return listResult.IsFailure
            ? ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<RepositoryListRecentResult>(listResult))
            : ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.Success(RepositoryListRecentResult.FromSnapshot(listResult.Data!)));
    }

    private async Task<ProtocolResponse> ProcessRepositoryRemoveRecentAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return MissingParameters(request.RequestId, RepositoryActionConstants.RemoveRecentAction);
        }

        var parametersResult =
            protocolSerializer.DeserializeParameters<RepositoryRemoveRecentParameters>(
                request.Parameters,
                RepositoryActionConstants.RemoveRecentAction);
        if (parametersResult.IsFailure ||
            !Guid.TryParseExact(parametersResult.Data?.RepositoryId, "D", out var repositoryId) ||
            !string.Equals(
                parametersResult.Data.RepositoryId,
                repositoryId.ToString("D"),
                StringComparison.Ordinal))
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.Validation(
                    "The repositories.removeRecent parameters are invalid.",
                    EngineErrorCode.InvalidRequest));
        }

        var result = await repositoryHistoryService.RemoveRecentAsync(
            repositoryId,
            cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }

    private async Task<ProtocolResponse> ProcessGetColorThemeAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        var preferenceResult = await colorThemePreferenceService.GetAsync(cancellationToken);
        if (preferenceResult.IsFailure)
        {
            return ProtocolResponseFactory.FromResult(
                request.RequestId,
                Result.ErrorFromResult<ColorThemePreferenceResult>(preferenceResult));
        }

        var value = preferenceResult.Data switch
        {
            ColorTheme.Light => ColorThemeResultValue.Light,
            ColorTheme.Dark => ColorThemeResultValue.Dark,
            null => (ColorThemeResultValue?)null,
            _ => throw new InvalidOperationException("The color-theme preference is not supported."),
        };
        return ProtocolResponseFactory.FromResult(
            request.RequestId,
            Result.Success(new ColorThemePreferenceResult(value)));
    }

    private async Task<ProtocolResponse> ProcessSetColorThemeAsync(
        EngineProtocolRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return MissingParameters(request.RequestId, PreferenceActionConstants.SetColorThemeAction);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ColorThemeSetParameters>(
            request.Parameters,
            PreferenceActionConstants.SetColorThemeAction);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var theme = parametersResult.Data!.ColorTheme switch
        {
            ColorThemeResultValue.Light => ColorTheme.Light,
            ColorThemeResultValue.Dark => ColorTheme.Dark,
            _ => throw new InvalidOperationException("The color-theme preference is not supported."),
        };
        var result = await colorThemePreferenceService.SetAsync(theme, cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }

    /// <summary>
    ///     Creates the standard validation failure for a parameterized action with omitted parameters.
    /// </summary>
    /// <param name="requestId">The correlated request identifier.</param>
    /// <param name="action">The fixed parameterized action.</param>
    /// <returns>The correlated invalid-request response.</returns>
    private static ProtocolResponse MissingParameters(string requestId, string action) =>
        ProtocolResponseFactory.FromError(
            requestId,
            OperationError.Validation(
                $"The {action} action requires parameters.",
                EngineErrorCode.InvalidRequest));

    private static bool IsKnownAction(string action) =>
        action is RepositoryActionConstants.OpenAction or
            RepositoryActionConstants.RestoreLastAction or
            RepositoryActionConstants.ListRecentAction or
            RepositoryActionConstants.RemoveRecentAction or
            ComparisonActionConstants.ListTargetsAction or
            ComparisonActionConstants.PrepareAction or
            ComparisonActionConstants.CheckFreshnessAction or
            PreferenceActionConstants.GetColorThemeAction or
            PreferenceActionConstants.SetColorThemeAction;

    /// <summary>
    ///     Determines whether an action belongs to the comparison boundary with restricted diagnostic context.
    /// </summary>
    /// <param name="action">The action name.</param>
    /// <returns><see langword="true" /> for an approved comparison action; otherwise, <see langword="false" />.</returns>
    private static bool IsComparisonAction(string action) =>
        action is ComparisonActionConstants.ListTargetsAction or
            ComparisonActionConstants.PrepareAction or
            ComparisonActionConstants.CheckFreshnessAction;

    /// <summary>
    ///     Logs one successful or expected failed action outcome.
    /// </summary>
    /// <param name="response">The action response.</param>
    /// <param name="request">The processed request.</param>
    /// <param name="elapsed">The elapsed action-processing time.</param>
    private void LogOutcome(
        ProtocolResponse response,
        EngineProtocolRequest request,
        TimeSpan elapsed)
    {
        if (response is ProtocolErrorResponse errorResponse)
        {
            logger.LogInformation(
                "Processed engine action {RequestId} for {Action} with errors {ErrorCodes} in " +
                "{ElapsedMilliseconds:0.000} ms.",
                request.RequestId,
                request.Action,
                errorResponse.Errors.Select(error => error.Code).ToArray(),
                elapsed.TotalMilliseconds);
            return;
        }

        logger.LogInformation(
            "Processed engine action {RequestId} for {Action} with a result in {ElapsedMilliseconds:0.000} ms.",
            request.RequestId,
            request.Action,
            elapsed.TotalMilliseconds);
    }
}
