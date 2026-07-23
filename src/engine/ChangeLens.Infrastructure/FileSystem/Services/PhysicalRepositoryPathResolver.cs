using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Infrastructure.FileSystem.Services;

/// <summary>
///     Resolves repository directory paths to their physical file-system locations.
/// </summary>
/// <remarks>
///     <para>
///         This implementation is stateless and safe to register as a singleton.
///     </para>
///     <para>
///         Each path segment is verified before it is used, so missing, inaccessible,
///         and link-bearing directories are distinguished consistently.
///     </para>
/// </remarks>
public sealed class PhysicalRepositoryPathResolver : IRepositoryPathResolver
{
    /// <inheritdoc />
    public async Task<Result<string>> ResolveAsync(
        string path,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            var fullPath = Path.GetFullPath(path);
            var root = Path.GetPathRoot(fullPath)
                ?? throw new ArgumentException("The selected directory does not have a file-system root.", nameof(path));
            var physicalPath = ResolveDirectory(root);
            var relativePath = fullPath[root.Length..];
            var segments = relativePath.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in segments)
            {
                cancellationToken.ThrowIfCancellationRequested();
                physicalPath = ResolveDirectory(Path.Combine(physicalPath, segment));
            }

            return Result.Success<string>(physicalPath);
        }
        catch (UnauthorizedAccessException)
        {
            return Result.Fail<string>(
                OperationError.Unauthorized(
                    "The selected directory cannot be accessed.",
                    RepositoryErrorCode.AccessDenied));
        }
        catch (Exception exception) when (
            exception is DirectoryNotFoundException or FileNotFoundException)
        {
            return Result.Fail<string>(
                OperationError.NotFound(
                    "The selected directory does not exist.",
                    RepositoryErrorCode.PathNotFound));
        }
        catch (Exception exception) when (
            exception is IOException or NotSupportedException or ArgumentException)
        {
            return Result.Fail<string>(
                OperationError.ExternalDependencyFailure(
                    "The selected directory could not be resolved.",
                    RepositoryErrorCode.InspectionFailed));
        }
    }

    private static string ResolveDirectory(string path)
    {
        var attributes = File.GetAttributes(path);

        if (!attributes.HasFlag(FileAttributes.Directory))
        {
            throw new FileNotFoundException("The selected path does not identify a directory.", path);
        }

        var directory = new DirectoryInfo(path);
        _ = directory.GetFileSystemInfos();
        var target = directory.ResolveLinkTarget(returnFinalTarget: true);

        if (target is null)
        {
            return directory.FullName;
        }

        var targetAttributes = File.GetAttributes(target.FullName);

        if (!targetAttributes.HasFlag(FileAttributes.Directory))
        {
            throw new FileNotFoundException("The selected path does not identify a directory.", target.FullName);
        }

        return target.FullName;
    }
}
