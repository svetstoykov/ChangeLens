using System.Diagnostics;
using ChangeLens.Core.Preferences.Interfaces;
using ChangeLens.Core.Preferences.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.LocalState.Helpers;
using ChangeLens.Infrastructure.LocalState.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace ChangeLens.Infrastructure.Preferences.Services;

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
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var applicationState = await context.ApplicationState
                .AsNoTracking()
                .Where(entry => entry.SingletonId == 1)
                .Select(entry => new { entry.ColorTheme })
                .SingleOrDefaultAsync(cancellationToken);

            if (applicationState is null)
            {
                logger.LogWarning(
                    "Failed to read color-theme preference: singleton application state row is missing after {ElapsedMilliseconds:0.000} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return LocalStateFailure.Invalid<ColorTheme?>();
            }

            logger.LogDebug("Queried stored color-theme preference value {StoredValue}.", applicationState.ColorTheme);
            var outcome = applicationState.ColorTheme switch
            {
                null => Result.Success<ColorTheme?>(null),
                "light" => Result.Success<ColorTheme?>(ColorTheme.Light),
                "dark" => Result.Success<ColorTheme?>(ColorTheme.Dark),
                _ => LogInvalidStoredTheme(logger, applicationState.ColorTheme),
            };

            if (outcome.IsSuccess)
            {
                logger.LogInformation(
                    "Read stored color-theme preference with outcome {Outcome} in {ElapsedMilliseconds:0.000} ms.",
                    outcome.Data is null ? "unset" : "set", Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            }

            return outcome;
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to read the stored color-theme preference after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable<ColorTheme?>();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to read the stored color-theme preference: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid<ColorTheme?>();
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
        var startedAt = Stopwatch.GetTimestamp();

        try
        {
            var applicationState = await context.ApplicationState.SingleOrDefaultAsync(entry => entry.SingletonId == 1, cancellationToken);
            if (applicationState is null)
            {
                logger.LogWarning(
                    "Failed to save color-theme preference: singleton application state row is missing after {ElapsedMilliseconds:0.000} ms.",
                    Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
                return LocalStateFailure.Invalid();
            }

            applicationState.ColorTheme = storedValue;
            await context.SaveChangesAsync(cancellationToken);
            logger.LogDebug("Updated stored color-theme preference to {ColorTheme}.", storedValue);
            logger.LogInformation(
                "Saved color-theme preference {ColorTheme} in {ElapsedMilliseconds:0.000} ms.",
                colorTheme, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return Result.Success();
        }
        catch (Exception exception) when (LocalStateFailure.IsExpectedAccessFailure(exception))
        {
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to save the color-theme preference {ColorTheme} after {ElapsedMilliseconds:0.000} ms.",
                colorTheme, Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Unavailable();
        }
        catch (Exception exception) when (LocalStateFailure.IsMalformedDataFailure(exception))
        {
            logger.LogDebug(exception, "SQLite local-state operation threw an exception.");
            logger.LogWarning(
                "Failed to save the color-theme preference: stored metadata is malformed after {ElapsedMilliseconds:0.000} ms.",
                Stopwatch.GetElapsedTime(startedAt).TotalMilliseconds);
            return LocalStateFailure.Invalid();
        }
    }

    private static Result<ColorTheme?> LogInvalidStoredTheme(ILogger<SqliteColorThemePreferenceStore> logger, string? storedValue)
    {
        logger.LogWarning("Stored color-theme preference {StoredValue} is not a recognized value.", storedValue);
        return LocalStateFailure.Invalid<ColorTheme?>();
    }
}
