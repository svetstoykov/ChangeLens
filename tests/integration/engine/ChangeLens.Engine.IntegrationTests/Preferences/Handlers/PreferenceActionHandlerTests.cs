using System.Text.Json;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.IntegrationTests.Preferences.Handlers.Support;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Preferences.Constants;
using ChangeLens.Engine.Preferences.Handlers;
using ChangeLens.Engine.Preferences.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Models;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Preferences.Handlers;

/// <summary>
///     Verifies defensive preference output mapping at the Engine protocol boundary.
/// </summary>
public sealed class PreferenceActionHandlerTests
{
    private const string RequestId = "preference-defensive-mapping";

    /// <summary>
    ///     Asynchronously verifies an unapproved stored theme returns the stable preference internal error.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnapprovedStoredColorThemeReturnsInternalError()
    {
        var handler = new PreferenceGetColorThemeHandler(
            new StubColorThemePreferenceService((ColorTheme)int.MaxValue));
        var request = CreateRequest(PreferenceActionConstants.GetColorThemeAction);

        var response = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

        AssertInternalError(response);
    }

    /// <summary>
    ///     Asynchronously verifies an unapproved protocol theme returns an internal error without reaching persistence.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task UnapprovedProtocolColorThemeReturnsInternalErrorWithoutPersistence()
    {
        var preferenceService = new StubColorThemePreferenceService(null);
        var handler = new PreferenceSetColorThemeHandler(
            preferenceService,
            new StubEngineProtocolSerializer(
                new ColorThemeSetParameters
                {
                    ColorTheme = (ColorThemeResultValue)int.MaxValue,
                }));
        var request = CreateRequest(
            PreferenceActionConstants.SetColorThemeAction,
            JsonSerializer.SerializeToElement(new { }));

        var response = await handler.HandleAsync(request, TestContext.Current.CancellationToken);

        AssertInternalError(response);
        Assert.False(preferenceService.SetCalled);
    }

    private static EngineProtocolRequest CreateRequest(string action, JsonElement parameters = default) =>
        new()
        {
            ProtocolVersion = EngineProtocolConstants.CurrentVersion,
            RequestId = RequestId,
            Action = action,
            Parameters = parameters,
        };

    private static void AssertInternalError(ProtocolResponse response)
    {
        var errorResponse = Assert.IsType<ProtocolErrorResponse>(response);
        Assert.Equal(RequestId, errorResponse.RequestId);
        var error = Assert.Single(errorResponse.Errors);
        Assert.Equal(ErrorType.InternalError, error.Type);
        Assert.Equal("preference.unmappedColorTheme", error.Code);
        Assert.Equal("The color-theme preference is not approved for the engine protocol.", error.Message);
    }
}
