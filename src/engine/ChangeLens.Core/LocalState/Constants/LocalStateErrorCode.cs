namespace ChangeLens.Core.LocalState.Constants;

/// <summary>
///     Provides stable error codes for local application state.
/// </summary>
public static class LocalStateErrorCode
{
    /// <summary>
    ///     The local-state database could not be accessed within its bounded limits.
    /// </summary>
    public const string Unavailable = "localState.unavailable";

    /// <summary>
    ///     A supported database migration failed and was rolled back.
    /// </summary>
    public const string MigrationFailed = "localState.migrationFailed";

    /// <summary>
    ///     The existing database is not valid ChangeLens local state.
    /// </summary>
    public const string Invalid = "localState.invalid";
}
