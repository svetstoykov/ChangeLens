namespace ChangeLens.Infrastructure.LocalState.Constants;

/// <summary>
///     Provides stable SQLite local-state configuration and schema values.
/// </summary>
public static class LocalStateConstants
{
    /// <summary>
    ///     The configuration key that specifies the local-state directory.
    /// </summary>
    public const string DirectoryConfigurationKey = "ChangeLens:LocalState:Directory";

    /// <summary>
    ///     The file name used for the local-state database.
    /// </summary>
    internal const string DatabaseFileName = "changelens.db";

    /// <summary>
    ///     The product name stored in local-state metadata.
    /// </summary>
    internal const string ProductName = "ChangeLens";

    /// <summary>
    ///     The local-state schema version currently supported by the application.
    /// </summary>
    internal const int CurrentSchemaVersion = 1;

    /// <summary>
    ///     The maximum number of seconds allowed for a local-state database command.
    /// </summary>
    internal const int CommandTimeoutSeconds = 5;

    /// <summary>
    ///     The maximum number of recent repositories retained in local state.
    /// </summary>
    internal const int MaximumRecentRepositories = 20;
}
