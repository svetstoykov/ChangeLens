using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Preferences.Constants;
using ChangeLens.Engine.Preferences.Interfaces;
using ChangeLens.Engine.Preferences.Models;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;
using ChangeLens.Engine.Protocol.Services;

namespace ChangeLens.Engine.Preferences.Handlers;

/// <summary>
///     Handles the payload-free action that reads the stored color-theme preference.
/// </summary>
/// <remarks>
///     The host registers this handler as a singleton. An absent preference is reported as <see langword="null" />
///     rather than as a failure, and supplied parameters are ignored exactly as before.
/// </remarks>
/// <param name="colorThemePreferenceService">The color-theme capability. Cannot be <see langword="null" />.</param>
internal sealed class PreferenceGetColorThemeHandler(IColorThemePreferenceService colorThemePreferenceService) : IActionHandler
{
    /// <inheritdoc />
    public string Action => PreferenceActionConstants.GetColorThemeAction;

    /// <inheritdoc />
    public async Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken)
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
        return ProtocolResponseFactory.FromResult(request.RequestId, Result.Success(new ColorThemePreferenceResult(value)));
    }
}
