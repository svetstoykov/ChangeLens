using ChangeLens.Core.Diagnostics.Services;
using ChangeLens.Core.Git.Interfaces;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ChangeLens.Infrastructure.FileSystem.Services;

/// <summary>
///     Resolves repository directory paths to their physical file-system locations.
/// </summary>
/// <remarks>
///     <para>
///         The Engine registers this implementation as scoped. It serves one request and does not need to be thread-safe.
///     </para>
///     <para>
///         Each path segment is verified before it is used, so missing, inaccessible,
///         and link-bearing directories are distinguished consistently.
///     </para>
/// </remarks>
public sealed class PhysicalRepositoryPathResolver : IRepositoryPathResolver
{
    private static readonly OperationError AccessDeniedError = OperationError.Unauthorized(
        "The selected directory cannot be accessed.",
        RepositoryErrorCode.AccessDenied);

    private static readonly OperationError PathNotFoundError = OperationError.NotFound(
        "The selected directory does not exist.",
        RepositoryErrorCode.PathNotFound);

    private static readonly OperationError ResolutionFailedError = OperationError.ExternalDependencyFailure(
        "The selected directory could not be resolved.",
        RepositoryErrorCode.InspectionFailed);

    private readonly ILogger<PhysicalRepositoryPathResolver> _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="PhysicalRepositoryPathResolver" /> class.
    /// </summary>
    /// <param name="logger">
    ///     The logger for path-resolution failures, or <see langword="null" /> to log nowhere.
    /// </param>
    public PhysicalRepositoryPathResolver(ILogger<PhysicalRepositoryPathResolver>? logger = null)
    {
        this._logger = logger ?? NullLogger<PhysicalRepositoryPathResolver>.Instance;
    }

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
        catch (UnauthorizedAccessException exception)
        {
            this._logger.LogDebug(
                exception,
                "Path resolution for {Path} failed: access denied.",
                PathSanitizer.RedactHomeDirectory(path));
            return AccessDeniedError;
        }
        catch (Exception exception) when (
            exception is DirectoryNotFoundException or FileNotFoundException)
        {
            this._logger.LogDebug(
                exception,
                "Path resolution for {Path} failed: the directory does not exist.",
                PathSanitizer.RedactHomeDirectory(path));
            return PathNotFoundError;
        }
        catch (Exception exception) when (
            exception is IOException or NotSupportedException or ArgumentException)
        {
            this._logger.LogWarning(exception, "Path resolution failed unexpectedly.");
            return ResolutionFailedError;
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
