namespace ChangeLens.Engine.IntegrationTests.Support;

/// <summary>
///     Provides paths to repository-owned integration-test inputs.
/// </summary>
internal static class RepositoryPaths
{
    /// <summary>
    ///     Gets the full path to the repository root.
    /// </summary>
    internal static string Root { get; } = FindRoot();

    /// <summary>
    ///     Gets the full path to the version 1 engine-protocol contracts.
    /// </summary>
    internal static string EngineProtocolV1 { get; } = Path.Combine(
        Root,
        "contracts",
        "engine-protocol",
        "v1");

    private static string FindRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "engine", "ChangeLens.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The ChangeLens repository root could not be located.");
    }
}
