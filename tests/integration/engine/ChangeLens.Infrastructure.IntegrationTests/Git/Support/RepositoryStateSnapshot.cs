using System.Security.Cryptography;
using System.Text;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Support;

/// <summary>
///     Represents content and porcelain-status evidence for a controlled repository state.
/// </summary>
internal sealed class RepositoryStateSnapshot
{
    private RepositoryStateSnapshot(
        IReadOnlyDictionary<string, string> fileHashes,
        IReadOnlyDictionary<string, string> categoryHashes,
        string porcelainStatus)
    {
        this.FileHashes = fileHashes;
        this.CategoryHashes = categoryHashes;
        this.PorcelainStatus = porcelainStatus;
    }

    /// <summary>
    ///     Gets SHA-256 content hashes keyed by stable root kind and relative file path.
    /// </summary>
    internal IReadOnlyDictionary<string, string> FileHashes { get; }

    /// <summary>
    ///     Gets stable content hashes for each repository state category relevant to read-only verification.
    /// </summary>
    internal IReadOnlyDictionary<string, string> CategoryHashes { get; }

    /// <summary>
    ///     Gets the exact exit and stream evidence from Git porcelain status.
    /// </summary>
    internal string PorcelainStatus { get; }

    /// <summary>
    ///     Captures the worktree, Git state categories, and porcelain status without recording access-time metadata.
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
        var gitDirectory = roots.GetValueOrDefault("git");
        var commonGitDirectory = roots.GetValueOrDefault("common") ?? gitDirectory;
        var categoryHashes = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["worktree"] = HashFiles(CaptureWorktreeFiles(roots.GetValueOrDefault("worktree"))),
            ["head"] = HashFiles(CapturePath("HEAD", gitDirectory)),
            ["refs"] = HashFiles(
                CapturePath("refs", commonGitDirectory)
                    .Concat(CapturePath("packed-refs", commonGitDirectory))),
            ["index"] = HashFiles(CapturePath("index", gitDirectory)),
            ["config"] = HashFiles(CapturePath("config", commonGitDirectory)),
            ["objects"] = HashFiles(CapturePath("objects", commonGitDirectory)),
            ["fetchHead"] = HashFiles(CapturePath("FETCH_HEAD", gitDirectory)),
            ["porcelain"] = HashText(porcelainStatus),
        };
        return new RepositoryStateSnapshot(hashes, categoryHashes, porcelainStatus);
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

    private static IReadOnlyDictionary<string, string> CaptureWorktreeFiles(string? worktreePath)
    {
        if (worktreePath is null || !Directory.Exists(worktreePath))
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var filePath in Directory.EnumerateFiles(worktreePath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(worktreePath, filePath)
                .Replace(Path.DirectorySeparatorChar, '/');
            if (relativePath == ".git" || relativePath.StartsWith(".git/", StringComparison.Ordinal))
            {
                continue;
            }

            hashes[relativePath] = HashFile(filePath);
        }

        return hashes;
    }

    private static IReadOnlyDictionary<string, string> CapturePath(string relativePath, string? rootPath)
    {
        if (rootPath is null)
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        var path = Path.Combine(rootPath, relativePath);
        if (File.Exists(path))
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal)
            {
                [relativePath] = HashFile(path),
            };
        }

        if (!Directory.Exists(path))
        {
            return new SortedDictionary<string, string>(StringComparer.Ordinal);
        }

        var hashes = new SortedDictionary<string, string>(StringComparer.Ordinal);
        CaptureFiles(relativePath, path, hashes);
        return hashes;
    }

    private static string HashFiles(IEnumerable<KeyValuePair<string, string>> fileHashes)
    {
        var builder = new StringBuilder();
        foreach (var pair in fileHashes.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            builder.Append(pair.Key);
            builder.Append('\0');
            builder.Append(pair.Value);
            builder.Append('\n');
        }

        return HashText(builder.ToString());
    }

    private static string HashFile(string filePath)
    {
        using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.ReadWrite | FileShare.Delete);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }

    private static string HashText(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
