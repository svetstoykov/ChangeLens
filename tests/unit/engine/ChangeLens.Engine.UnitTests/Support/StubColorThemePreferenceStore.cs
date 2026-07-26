using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Engine.UnitTests.Support;

internal sealed class StubColorThemePreferenceStore : IColorThemePreferenceStore
{
    public Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success<ColorTheme?>(null));

    public Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Success());
}
