using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores the explicit color-theme preference in SQLite local state.
/// </summary>
/// <param name="database">The required SQLite local-state database.</param>
public sealed class SqliteColorThemePreferenceStore(ILocalStateDatabase database) : IColorThemePreferenceStore
{
    /// <inheritdoc />
    public async Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var context = await database.CreateContextAsync(cancellationToken);
            var storedValue = await context.ApplicationState
                .Where(entry => entry.SingletonId == 1)
                .Select(entry => entry.ColorTheme)
                .SingleAsync(cancellationToken);
            return storedValue switch
            {
                null => Result.Success<ColorTheme?>(null),
                "light" => Result.Success<ColorTheme?>(ColorTheme.Light),
                "dark" => Result.Success<ColorTheme?>(ColorTheme.Dark),
                _ => SqliteLocalStateDatabase.Invalid<ColorTheme?>(),
            };
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable<ColorTheme?>(exception);
        }
    }

    /// <inheritdoc />
    public async Task<Result> SetAsync(ColorTheme colorTheme, CancellationToken cancellationToken)
    {
        var storedValue = colorTheme switch
        {
            ColorTheme.Light => "light",
            ColorTheme.Dark => "dark",
            _ => throw new ArgumentOutOfRangeException(nameof(colorTheme)),
        };

        try
        {
            await using var context = await database.CreateContextAsync(cancellationToken);
            var applicationState = await context.ApplicationState.SingleAsync(
                entry => entry.SingletonId == 1,
                cancellationToken);
            applicationState.ColorTheme = storedValue;
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable(exception);
        }
    }
}
