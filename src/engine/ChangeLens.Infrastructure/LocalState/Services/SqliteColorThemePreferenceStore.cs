using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores the explicit color-theme preference in SQLite local state.
/// </summary>
/// <remarks>
///     The Engine registers this implementation as scoped. It uses the request context and does not need to be thread-safe.
/// </remarks>
/// <param name="context">The scoped local-state context. Cannot be <see langword="null" />.</param>
/// <param name="logger">The logger for color-theme persistence failures.</param>
public sealed class SqliteColorThemePreferenceStore(
    ChangeLensLocalStateDbContext context,
    ILogger<SqliteColorThemePreferenceStore> logger) : IColorThemePreferenceStore
{
    /// <inheritdoc />
    public async Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            var storedValue = await context.ApplicationState
                .AsNoTracking()
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
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to read the stored color-theme preference.");
            return LocalStateFailure.Unavailable<ColorTheme?>(exception);
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
            var applicationState = await context.ApplicationState.SingleAsync(entry => entry.SingletonId == 1, cancellationToken);
            applicationState.ColorTheme = storedValue;
            await context.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            logger.LogWarning(exception, "Failed to save the color-theme preference {ColorTheme}.", colorTheme);
            return LocalStateFailure.Unavailable(exception);
        }
    }

    private static Result<ColorTheme?> LogInvalidStoredTheme(ILogger<SqliteColorThemePreferenceStore> logger, string? storedValue)
    {
        logger.LogWarning("Stored color-theme preference {StoredValue} is not a recognized value.", storedValue);
        return LocalStateFailure.Invalid<ColorTheme?>();
    }
}
