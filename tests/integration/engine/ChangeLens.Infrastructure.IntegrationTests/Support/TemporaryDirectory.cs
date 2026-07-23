namespace ChangeLens.Infrastructure.IntegrationTests.Support;

/// <summary>
///     Represents a unique temporary directory that is deleted when disposed.
/// </summary>
internal sealed class TemporaryDirectory : IDisposable
{
    /// <summary>
    ///     Initializes a new instance of the <see cref="TemporaryDirectory" /> class.
    /// </summary>
    public TemporaryDirectory()
    {
        DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Infrastructure.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(DirectoryPath);
    }

    /// <summary>
    ///     Gets the full path to the temporary directory.
    /// </summary>
    public string DirectoryPath { get; }

    /// <summary>
    ///     Deletes the temporary directory and its contents.
    /// </summary>
    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DirectoryPath))
            {
                Directory.Delete(DirectoryPath, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
