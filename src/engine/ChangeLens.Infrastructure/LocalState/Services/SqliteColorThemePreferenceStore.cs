using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.LocalState.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Infrastructure.LocalState.Services;

/// <summary>
///     Stores the explicit color-theme preference in SQLite local state.
/// </summary>
/// <param name="database">The required SQLite local-state database.</param>
public sealed class SqliteColorThemePreferenceStore(SqliteLocalStateDatabase database)
    : IColorThemePreferenceStore
{
    /// <inheritdoc />
    public async Task<Result<ColorTheme?>> GetAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText =
                "SELECT color_theme FROM application_state WHERE singleton_id = 1;";
            var value = await command.ExecuteScalarAsync(cancellationToken);
            return value switch
            {
                null or DBNull => Result.Success<ColorTheme?>(null),
                "light" => Result.Success<ColorTheme?>(ColorTheme.Light),
                "dark" => Result.Success<ColorTheme?>(ColorTheme.Dark),
                _ => Result.Fail<ColorTheme?>(
                    OperationError.UnprocessableInput(
                        "The stored color-theme preference is invalid.",
                        Core.LocalState.Constants.LocalStateErrorCode.Invalid)),
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
            await using var connection = await database.OpenConnectionAsync(cancellationToken);
            await using var transaction = connection.BeginTransaction();
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                "UPDATE application_state SET color_theme = $colorTheme WHERE singleton_id = 1;";
            command.Parameters.AddWithValue("$colorTheme", storedValue);
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception exception) when (SqliteLocalStateDatabase.IsExpectedAccessFailure(exception))
        {
            return SqliteLocalStateDatabase.Unavailable(exception);
        }
    }
}
