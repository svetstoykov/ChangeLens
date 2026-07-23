using System.Security.Cryptography;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Support;

/// <summary>
///     Represents content and porcelain-status evidence for a controlled repository state.
/// </summary>
internal sealed class RepositoryStateSnapshot
{
    private RepositoryStateSnapshot(
        IReadOnlyDictionary<string, string> fileHashes,
        string porcelainStatus)
    {
        FileHashes = fileHashes;
        PorcelainStatus = porcelainStatus;
    }

    /// <summary>
    ///     Gets SHA-256 content hashes keyed by stable root kind and relative file path.
    /// </summary>
    internal IReadOnlyDictionary<string, string> FileHashes { get; }

    /// <summary>
    ///     Gets the exact exit and stream evidence from Git porcelain status.
    /// </summary>
    internal string PorcelainStatus { get; }

    /// <summary>
    ///     Captures the selected directory, worktree, Git directory, common Git directory, and porcelain status.
    /// </summary>
    /// <param name="selectedPath">The selected directory to capture. Cannot be <see langword="null" /> or empty.</param>
    /// <returns>The content and status snapshot.</returns>
    internal static RepositoryStateSnapshot Capture(string selectedPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(selectedPath);
        var fullSelectedPath = Path.GetFullPath(selectedPath);
        var roots = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["selected"] = fullSelectedPath,
        };

        var topLevel = TemporaryGitRepository.RunGit(
            ["-C", fullSelectedPath, "rev-parse", "--show-toplevel"]);
        if (topLevel.ExitCode == 0)
        {
            roots["worktree"] = topLevel.StandardOutput.Trim();
        }

        AddMetadataRoot(
            roots,
            "git",
            fullSelectedPath,
            TemporaryGitRepository.RunGit(
                ["-C", fullSelectedPath, "rev-parse", "--git-dir"]));
        AddMetadataRoot(
            roots,
            "common",
            fullSelectedPath,
            TemporaryGitRepository.RunGit(
                ["-C", fullSelectedPath, "rev-parse", "--git-common-dir"]));

        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var root in roots)
        {
            CaptureFiles(root.Key, root.Value, hashes);
        }

        var status = TemporaryGitRepository.RunGit(
            ["-C", fullSelectedPath, "status", "--porcelain=v1", "--untracked-files=all"]);
        var porcelainStatus = string.Join(
            "\n",
            status.ExitCode.ToString(System.Globalization.CultureInfo.InvariantCulture),
            status.StandardOutput,
            status.StandardError);
        return new RepositoryStateSnapshot(hashes, porcelainStatus);
    }

    private static void AddMetadataRoot(
        IDictionary<string, string> roots,
        string key,
        string selectedPath,
        (int ExitCode, string StandardOutput, string StandardError) output)
    {
        if (output.ExitCode != 0)
        {
            return;
        }

        var reportedPath = output.StandardOutput.Trim();
        roots[key] = Path.GetFullPath(reportedPath, selectedPath);
    }

    private static void CaptureFiles(
        string rootKind,
        string rootPath,
        IDictionary<string, string> hashes)
    {
        if (!Directory.Exists(rootPath))
        {
            return;
        }

        foreach (var filePath in Directory.EnumerateFiles(
                     rootPath,
                     "*",
                     SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(rootPath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            using var stream = new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            var hash = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
            hashes[$"{rootKind}:{relativePath}"] = hash;
        }
    }
}
