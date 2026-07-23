using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using Xunit;
using Xunit.Sdk;

namespace ChangeLens.Infrastructure.IntegrationTests.FileSystem.Services;

/// <summary>
///     Verifies physical repository path resolution against the local file system.
/// </summary>
public sealed class PhysicalRepositoryPathResolverTests
{
    /// <summary>
    ///     Asynchronously resolves a relative directory to its normalized absolute path.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_RelativeDirectory_ReturnsAbsoluteNormalizedDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var resolver = new PhysicalRepositoryPathResolver();
        var previousDirectory = Directory.GetCurrentDirectory();

        try
        {
            Directory.SetCurrentDirectory(temporaryDirectory.DirectoryPath);

            var result = await resolver.ResolveAsync(".", CancellationToken.None);

            Assert.True(result.IsSuccess);
            var physicalPath = Assert.IsType<string>(result.Data);
            Assert.True(Path.IsPathFullyQualified(physicalPath));
            Assert.EndsWith(Path.GetFileName(temporaryDirectory.DirectoryPath), physicalPath);
        }
        finally
        {
            Directory.SetCurrentDirectory(previousDirectory);
        }
    }

    /// <summary>
    ///     Asynchronously resolves a directory path with dot segments to its physical directory.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_DirectoryWithDotSegments_ReturnsCollapsedDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var targetDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.DirectoryPath, "target"));
        Directory.CreateDirectory(Path.Combine(targetDirectory.FullName, "child"));
        var resolver = new PhysicalRepositoryPathResolver();

        var result = await resolver.ResolveAsync(
            Path.Combine(targetDirectory.FullName, "child", ".."),
            CancellationToken.None);

        Assert.True(result.IsSuccess);
        var physicalPath = Assert.IsType<string>(result.Data);
        Assert.True(Path.IsPathFullyQualified(physicalPath));
        Assert.DoesNotContain("..", physicalPath);
        Assert.EndsWith(Path.DirectorySeparatorChar + targetDirectory.Name, physicalPath);
    }

    /// <summary>
    ///     Asynchronously resolves a directory link to its target directory.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_DirectoryLink_ReturnsLinkTarget()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var targetDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.DirectoryPath, "target"));
        var linkPath = Path.Combine(temporaryDirectory.DirectoryPath, "link");
        CreateDirectoryLinkOrSkip(linkPath, targetDirectory.FullName);
        var resolver = new PhysicalRepositoryPathResolver();

        var result = await resolver.ResolveAsync(linkPath, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(targetDirectory.FullName, result.Data);
    }

    /// <summary>
    ///     Asynchronously resolves a path beneath a directory link to its physical child directory.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_LinkInIntermediateSegment_ReturnsPhysicalChildDirectory()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var targetDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.DirectoryPath, "target"));
        var physicalChildDirectory = Directory.CreateDirectory(Path.Combine(targetDirectory.FullName, "child"));
        var linkPath = Path.Combine(temporaryDirectory.DirectoryPath, "link");
        CreateDirectoryLinkOrSkip(linkPath, targetDirectory.FullName);
        var resolver = new PhysicalRepositoryPathResolver();

        var result = await resolver.ResolveAsync(Path.Combine(linkPath, "child"), CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(physicalChildDirectory.FullName, result.Data);
    }

    /// <summary>
    ///     Asynchronously resolves a file-system root without changing its path.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_FileSystemRoot_ReturnsRoot()
    {
        var root = Path.GetPathRoot(Path.GetTempPath());
        Assert.NotNull(root);
        var resolver = new PhysicalRepositoryPathResolver();

        var result = await resolver.ResolveAsync(root, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.Equal(root, result.Data);
    }

    /// <summary>
    ///     Asynchronously returns the path-not-found error for a missing path.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_MissingPath_ReturnsNotFoundError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var resolver = new PhysicalRepositoryPathResolver();

        var result = await resolver.ResolveAsync(
            Path.Combine(temporaryDirectory.DirectoryPath, "missing"),
            CancellationToken.None);

        AssertFailure(result, ErrorType.NotFound, RepositoryErrorCode.PathNotFound);
    }

    /// <summary>
    ///     Asynchronously returns the path-not-found error for a file path.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_FilePath_ReturnsNotFoundError()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var filePath = Path.Combine(temporaryDirectory.DirectoryPath, "file.txt");
        await File.WriteAllTextAsync(filePath, "contents", TestContext.Current.CancellationToken);
        var resolver = new PhysicalRepositoryPathResolver();

        var result = await resolver.ResolveAsync(filePath, CancellationToken.None);

        AssertFailure(result, ErrorType.NotFound, RepositoryErrorCode.PathNotFound);
    }

    /// <summary>
    ///     Asynchronously returns the access-denied error for an inaccessible directory.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_InaccessibleDirectory_ReturnsUnauthorizedError()
    {
        if (OperatingSystem.IsWindows())
        {
            throw SkipException.ForSkip("The integration fixture cannot reliably deny directory access on Windows.");
        }

        using var temporaryDirectory = new TemporaryDirectory();
        var inaccessibleDirectory = Directory.CreateDirectory(Path.Combine(temporaryDirectory.DirectoryPath, "inaccessible"));
        File.SetUnixFileMode(
            inaccessibleDirectory.FullName,
            UnixFileMode.UserWrite);

        try
        {
            try
            {
                _ = new DirectoryInfo(inaccessibleDirectory.FullName).GetFileSystemInfos();
            }
            catch (UnauthorizedAccessException)
            {
                var resolver = new PhysicalRepositoryPathResolver();

                var result = await resolver.ResolveAsync(inaccessibleDirectory.FullName, CancellationToken.None);

                AssertFailure(result, ErrorType.Unauthorized, RepositoryErrorCode.AccessDenied);
                return;
            }

            throw SkipException.ForSkip("The current process retained access to the permission-restricted directory.");
        }
        finally
        {
            File.SetUnixFileMode(
                inaccessibleDirectory.FullName,
                UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }
    }

    /// <summary>
    ///     Asynchronously throws when cancellation is requested before path resolution.
    /// </summary>
    /// <returns>
    ///     A task that represents the asynchronous operation.
    /// </returns>
    [Fact]
    public async Task ResolveAsync_CanceledToken_ThrowsOperationCanceledException()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.Cancel();
        var resolver = new PhysicalRepositoryPathResolver();

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => resolver.ResolveAsync(temporaryDirectory.DirectoryPath, cancellationTokenSource.Token));
    }

    private static void AssertFailure(
        Result<string> result,
        ErrorType expectedType,
        string expectedCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedType, error.Type);
        Assert.Equal(expectedCode, error.Code);
    }

    private static void CreateDirectoryLinkOrSkip(string linkPath, string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (
            exception is IOException or UnauthorizedAccessException or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip(
                $"The operating system refused the directory-link fixture: {exception.Message}");
        }
    }
}
