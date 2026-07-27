using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores the explicit color-theme preference in SQLite local state.
/// </summary>
/// <param name="database">The required SQLite local-state database.</param>
/// <param name="logger">The logger for color-theme persistence failures.</param>
public sealed class SqliteColorThemePreferenceStore(
    ILocalStateDatabase database,
    ILogger<SqliteColorThemePreferenceStore> logger) : IColorThemePreferenceStore
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
                _ => LogInvalidStoredTheme(logger, storedValue),
            };
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to read the stored color-theme preference.");
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
            logger.LogWarning(exception, "Failed to save the color-theme preference {ColorTheme}.", colorTheme);
            return SqliteLocalStateDatabase.Unavailable(exception);
        }
    }

    private static Result<ColorTheme?> LogInvalidStoredTheme(
        ILogger<SqliteColorThemePreferenceStore> logger,
        string? storedValue)
    {
        logger.LogWarning(
            "Stored color-theme preference {StoredValue} is not a recognized value.",
            storedValue);
        return SqliteLocalStateDatabase.Invalid<ColorTheme?>();
    }
}
