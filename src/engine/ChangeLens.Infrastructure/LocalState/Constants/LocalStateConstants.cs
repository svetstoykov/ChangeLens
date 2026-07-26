namespace ChangeLens.Infrastructure.LocalState.Constants;

/// <summary>
///     Provides stable SQLite local-state configuration and schema values.
/// </summary>
internal static class LocalStateConstants
{
    internal const string DirectoryConfigurationKey = "ChangeLens:LocalState:Directory";
    internal const string DatabaseFileName = "changelens.db";
    internal const string ProductName = "ChangeLens";
    internal const int CurrentSchemaVersion = 1;
    internal const int CommandTimeoutSeconds = 5;
    internal const int MaximumRecentRepositories = 20;
    internal const string InitialMigrationId = "20260726150843_InitialLocalState";
}
