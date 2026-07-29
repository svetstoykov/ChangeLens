using System.Text.Json;
using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Preferences.Constants;
using ChangeLens.Engine.Preferences.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Preferences.Handlers;

/// <summary>
///     Handles the action that stores an explicit color-theme preference.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. A theme value the protocol has not approved is returned as a
///     domain-coded internal error without changing the stored preference.
/// </remarks>
/// <param name="colorThemePreferenceService">The color-theme capability. Cannot be <see langword="null" />.</param>
/// <param name="protocolSerializer">The strict engine protocol serializer. Cannot be <see langword="null" />.</param>
internal sealed class PreferenceSetColorThemeHandler(
    IColorThemePreferenceService colorThemePreferenceService,
    IEngineProtocolSerializer protocolSerializer) : IActionHandler
{
    /// <inheritdoc />
    public string Action => PreferenceActionConstants.SetColorThemeAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
    {
        if (request.Parameters.ValueKind == JsonValueKind.Undefined)
        {
            return ProtocolResponseFactory.MissingParameters(request.RequestId, this.Action);
        }

        var parametersResult = protocolSerializer.DeserializeParameters<ColorThemeSetParameters>(request.Parameters, this.Action);
        if (parametersResult.IsFailure)
        {
            return ProtocolResponseFactory.CreateError(request.RequestId, parametersResult.Errors);
        }

        var colorTheme = parametersResult.Data!.ColorTheme;
        if (colorTheme is not (ColorThemeResultValue.Light or ColorThemeResultValue.Dark))
        {
            return ProtocolResponseFactory.FromError(
                request.RequestId,
                OperationError.InternalError(
                    "The color-theme preference is not approved for the engine protocol.",
                    PreferenceErrorCode.UnmappedColorTheme));
        }

        var theme = colorTheme == ColorThemeResultValue.Light
            ? ColorTheme.Light
            : ColorTheme.Dark;
        var result = await colorThemePreferenceService.SetAsync(theme, cancellationToken);
        return ProtocolResponseFactory.FromResult(request.RequestId, result);
    }
}
