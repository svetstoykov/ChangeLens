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
        this.DirectoryPath = Path.Combine(
            Path.GetTempPath(),
            "ChangeLens.Infrastructure.IntegrationTests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(this.DirectoryPath);
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
            if (Directory.Exists(this.DirectoryPath))
            {
                Directory.Delete(this.DirectoryPath, recursive: true);
            }
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException)
        {
        }
    }
}
