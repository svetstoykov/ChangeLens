using System.Diagnostics;
using System.Globalization;
using ChangeLens.Infrastructure.IntegrationTests.Support;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Support;

/// <summary>
///     Represents a controlled temporary Git repository used by infrastructure integration tests.
/// </summary>
internal sealed class TemporaryGitRepository : IDisposable
{
    private static readonly TimeSpan ProcessTimeout = TimeSpan.FromSeconds(15);
    private readonly TemporaryDirectory _temporaryDirectory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="TemporaryGitRepository" /> class with an initial commit.
    /// </summary>
    /// <param name="directoryName">
    ///     The repository directory name beneath the temporary fixture root. Cannot be <see langword="null" /> or empty.
    /// </param>
    /// <param name="objectFormat">
    ///     The Git object format, or <see langword="null" /> to use the installed Git default.
    /// </param>
    public TemporaryGitRepository(
        string directoryName = "repository",
        string? objectFormat = null)
        : this(directoryName, objectFormat, bare: false, createInitialCommit: true)
    {
    }

    private TemporaryGitRepository(
        string directoryName,
        string? objectFormat,
        bool bare,
        bool createInitialCommit)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directoryName);
        this._temporaryDirectory = new TemporaryDirectory();
        this.RootPath = Path.Combine(this._temporaryDirectory.DirectoryPath, directoryName);

        var initArguments = new List<string> { "init" };
        if (!bare)
        {
            initArguments.Add("--initial-branch=main");
        }

        if (objectFormat is not null)
        {
            initArguments.Add($"--object-format={objectFormat}");
        }

        if (bare)
        {
            initArguments.Add("--bare");
        }

        initArguments.Add(this.RootPath);
        RunGitChecked(initArguments);

        if (bare)
        {
            return;
        }

        ConfigureRepository(this.RootPath);
        if (createInitialCommit)
        {
            CommitFixture(this.RootPath, "fixture.txt", "initial fixture content\n", "initial commit");
        }
    }

    /// <summary>
    ///     Gets the repository working-tree or bare-repository root.
    /// </summary>
    internal string RootPath { get; }

    /// <summary>
    ///     Gets the full committed HEAD object identifier.
    /// </summary>
    internal string Revision =>
        RunGitChecked(["-C", this.RootPath, "rev-parse", "--verify", "HEAD"]).StandardOutput.Trim();

    /// <summary>
    ///     Creates a controlled bare repository.
    /// </summary>
    /// <returns>A temporary bare repository.</returns>
    internal static TemporaryGitRepository CreateBare() =>
        new("bare-repository", objectFormat: null, bare: true, createInitialCommit: false);

    /// <summary>
    ///     Creates a controlled repository without a committed HEAD.
    /// </summary>
    /// <returns>A temporary unborn repository.</returns>
    internal static TemporaryGitRepository CreateUnborn() =>
        new("unborn-repository", objectFormat: null, bare: false, createInitialCommit: false);

    /// <summary>
    ///     Determines whether the installed Git supports the given object format.
    /// </summary>
    /// <param name="objectFormat">The object format to probe. Cannot be <see langword="null" /> or empty.</param>
    /// <returns>
    ///     <see langword="true" /> when Git initializes a repository with the format; otherwise,
    ///     <see langword="false" />.
    /// </returns>
    internal static bool SupportsObjectFormat(string objectFormat)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(objectFormat);
        using var temporaryDirectory = new TemporaryDirectory();
        var repositoryPath = Path.Combine(temporaryDirectory.DirectoryPath, "format-probe");
        var output = RunGit(["init", $"--object-format={objectFormat}", repositoryPath]);
        return output.ExitCode == 0;
    }

    /// <summary>
    ///     Creates and returns a nested directory beneath the repository root.
    /// </summary>
    /// <returns>The full nested-directory path.</returns>
    internal string CreateNestedDirectory() =>
        Directory.CreateDirectory(Path.Combine(this.RootPath, "nested", "directory")).FullName;

    /// <summary>
    ///     Creates a local branch at the current committed revision without checking it out.
    /// </summary>
    /// <param name="branchName">The branch name. Cannot be <see langword="null" /> or empty.</param>
    internal void CreateLocalBranch(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        RunGitChecked(["-C", this.RootPath, "branch", branchName, this.Revision]);
    }

    /// <summary>
    ///     Creates a direct cached remote-tracking ref at the current committed revision without fetching.
    /// </summary>
    /// <param name="remoteName">The remote name. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="branchName">The remote branch name. Cannot be <see langword="null" /> or empty.</param>
    internal void CreateRemoteTrackingBranch(
        string remoteName,
        string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "update-ref",
                $"refs/remotes/{remoteName}/{branchName}", this.Revision,
            ]);
    }

    /// <summary>
    ///     Creates a symbolic cached remote HEAD without contacting a remote.
    /// </summary>
    /// <param name="remoteName">The remote name. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="branchName">The cached branch selected by the symbolic ref.</param>
    internal void CreateSymbolicRemoteHead(
        string remoteName,
        string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "symbolic-ref",
                $"refs/remotes/{remoteName}/HEAD",
                $"refs/remotes/{remoteName}/{branchName}",
            ]);
    }

    /// <summary>
    ///     Configures a local branch to track a named remote branch without fetching.
    /// </summary>
    /// <param name="branchName">The local branch name. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="remoteName">The configured remote name. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="remoteBranchName">The remote branch name. Cannot be <see langword="null" /> or empty.</param>
    internal void ConfigureBranchUpstream(
        string branchName,
        string remoteName,
        string remoteBranchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteName);
        ArgumentException.ThrowIfNullOrWhiteSpace(remoteBranchName);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "config",
                $"remote.{remoteName}.url",
                $"https://example.invalid/{remoteName}.git",
            ]);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "config",
                $"remote.{remoteName}.fetch",
                $"+refs/heads/*:refs/remotes/{remoteName}/*",
            ]);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "config",
                $"branch.{branchName}.remote",
                remoteName,
            ]);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "config",
                $"branch.{branchName}.merge",
                $"refs/heads/{remoteBranchName}",
            ]);
    }

    /// <summary>
    ///     Adds a bare origin remote and establishes the cached remote-tracking baseline via one real push and one
    ///     real fetch, exactly as a developer's initial clone and first fetch would.
    /// </summary>
    /// <param name="bareOrigin">The bare repository acting as the server. Cannot be <see langword="null" />.</param>
    /// <param name="branchName">The branch pushed and fetched. Cannot be <see langword="null" /> or empty.</param>
    internal void AddOrigin(
        TemporaryGitRepository bareOrigin,
        string branchName = "main")
    {
        ArgumentNullException.ThrowIfNull(bareOrigin);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        RunGitChecked(["-C", this.RootPath, "remote", "add", "origin", bareOrigin.RootPath]);
        RunGitChecked(["-C", this.RootPath, "push", "--quiet", "origin", $"HEAD:refs/heads/{branchName}"]);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "fetch", "--quiet", "origin", $"{branchName}:refs/remotes/origin/{branchName}",
            ]);
    }

    /// <summary>
    ///     Adds an origin remote pointing at a nonexistent local path and seeds a cached remote-tracking ref without
    ///     fetching, simulating a repository whose remote has since become unreachable.
    /// </summary>
    /// <param name="branchName">The branch given a cached remote-tracking ref. Cannot be <see langword="null" /> or empty.</param>
    internal void AddUnreachableOrigin(string branchName = "main")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        var missingPath = Path.Combine(this._temporaryDirectory.DirectoryPath, "does-not-exist", "origin.git");
        RunGitChecked(["-C", this.RootPath, "remote", "add", "origin", missingPath]);
        RunGitChecked(
            ["-C", this.RootPath, "update-ref", $"refs/remotes/origin/{branchName}", this.Revision]);
    }

    /// <summary>
    ///     Pushes one new commit directly into a bare origin from an independent clone, simulating a colleague's
    ///     push that the caller's own clone has not yet fetched.
    /// </summary>
    /// <param name="bareOrigin">The bare repository acting as the server. Cannot be <see langword="null" />.</param>
    /// <param name="branchName">The branch pushed. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="relativePath">The repository-relative file path committed. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="content">The exact file content. Cannot be <see langword="null" />.</param>
    /// <param name="message">The commit message. Cannot be <see langword="null" /> or empty.</param>
    /// <returns>The full object identifier of the commit pushed to the bare origin.</returns>
    internal static string PushBehindCallersBack(
        TemporaryGitRepository bareOrigin,
        string branchName,
        string relativePath,
        string content,
        string message)
    {
        ArgumentNullException.ThrowIfNull(bareOrigin);
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        using var colleagueDirectory = new TemporaryDirectory();
        var colleaguePath = Path.Combine(colleagueDirectory.DirectoryPath, "colleague-clone");
        RunGitChecked(["clone", "--quiet", "--branch", branchName, bareOrigin.RootPath, colleaguePath]);
        ConfigureRepository(colleaguePath);
        File.WriteAllText(Path.Combine(colleaguePath, relativePath), content);
        RunGitChecked(["-C", colleaguePath, "add", "--", relativePath]);
        RunGitChecked(["-C", colleaguePath, "commit", "--quiet", "--no-gpg-sign", "-m", message]);
        RunGitChecked(["-C", colleaguePath, "push", "--quiet", "origin", branchName]);
        return RunGitChecked(["-C", colleaguePath, "rev-parse", "--verify", "HEAD"]).StandardOutput.Trim();
    }

    /// <summary>
    ///     Detaches HEAD at the current committed revision.
    /// </summary>
    internal void CheckoutDetached()
    {
        RunGitChecked(["-C", this.RootPath, "checkout", "--detach", "--quiet"]);
    }

    /// <summary>
    ///     Checks out an existing local branch.
    /// </summary>
    /// <param name="branchName">The branch name. Cannot be <see langword="null" /> or empty.</param>
    internal void CheckoutBranch(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        RunGitChecked(["-C", this.RootPath, "checkout", "--quiet", branchName]);
    }

    /// <summary>
    ///     Writes, stages, and commits one repository-relative file.
    /// </summary>
    /// <param name="relativePath">The repository-relative file path. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="content">The exact file content. Cannot be <see langword="null" />.</param>
    /// <param name="message">The commit message. Cannot be <see langword="null" /> or empty.</param>
    internal void CommitFile(
        string relativePath,
        string content,
        string message)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);
        var filePath = Path.Combine(this.RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
        RunGitChecked(["-C", this.RootPath, "add", "--", relativePath]);
        RunGitChecked(
            ["-C", this.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", message]);
    }

    /// <summary>
    ///     Writes one unstaged repository-relative file.
    /// </summary>
    /// <param name="relativePath">The repository-relative file path. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="content">The exact file content. Cannot be <see langword="null" />.</param>
    internal void WriteFile(
        string relativePath,
        string content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentNullException.ThrowIfNull(content);
        var filePath = Path.Combine(this.RootPath, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
    }

    /// <summary>
    ///     Stages one repository-relative path.
    /// </summary>
    /// <param name="relativePath">The repository-relative path. Cannot be <see langword="null" /> or empty.</param>
    internal void Stage(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        RunGitChecked(["-C", this.RootPath, "add", "--", relativePath]);
    }

    /// <summary>
    ///     Moves and stages one repository-relative path.
    /// </summary>
    /// <param name="sourcePath">The current repository-relative path. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="destinationPath">The new repository-relative path. Cannot be <see langword="null" /> or empty.</param>
    internal void Move(
        string sourcePath,
        string destinationPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(destinationPath);
        RunGitChecked(["-C", this.RootPath, "mv", "--", sourcePath, destinationPath]);
    }

    /// <summary>
    ///     Removes and stages one repository-relative path.
    /// </summary>
    /// <param name="relativePath">The repository-relative path. Cannot be <see langword="null" /> or empty.</param>
    internal void Remove(string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        RunGitChecked(["-C", this.RootPath, "rm", "--quiet", "--", relativePath]);
    }

    /// <summary>
    ///     Replaces a tracked regular file with a staged symbolic link.
    /// </summary>
    /// <param name="relativePath">The repository-relative path. Cannot be <see langword="null" /> or empty.</param>
    /// <param name="targetPath">The symbolic-link target text. Cannot be <see langword="null" /> or empty.</param>
    internal void ReplaceWithSymbolicLink(
        string relativePath,
        string targetPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetPath);
        var filePath = Path.Combine(this.RootPath, relativePath);
        File.Delete(filePath);
        File.CreateSymbolicLink(filePath, targetPath);
        this.Stage(relativePath);
    }

    /// <summary>
    ///     Creates a local branch whose commit belongs to an unrelated root history.
    /// </summary>
    /// <param name="branchName">The branch name. Cannot be <see langword="null" /> or empty.</param>
    internal void CreateUnrelatedBranch(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        var tree = RunGitChecked(
            ["-C", this.RootPath, "rev-parse", "--verify", "HEAD^{tree}"]).StandardOutput.Trim();
        var commit = RunGitChecked(
            ["-C", this.RootPath, "commit-tree", tree, "-m", "unrelated root"]).StandardOutput.Trim();
        RunGitChecked(
            ["-C", this.RootPath, "update-ref", $"refs/heads/{branchName}", commit]);
    }

    /// <summary>
    ///     Creates a target and current revision with two best criss-cross merge bases.
    /// </summary>
    /// <param name="targetBranchName">The target branch name. Cannot be <see langword="null" /> or empty.</param>
    internal void CreateCrissCrossHistory(string targetBranchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetBranchName);
        var root = this.Revision;
        var tree = RunGitChecked(
            ["-C", this.RootPath, "rev-parse", "--verify", "HEAD^{tree}"]).StandardOutput.Trim();
        var firstCurrent = RunGitChecked(
            ["-C", this.RootPath, "commit-tree", tree, "-p", root, "-m", "first current"]).StandardOutput.Trim();
        var firstTarget = RunGitChecked(
            ["-C", this.RootPath, "commit-tree", tree, "-p", root, "-m", "first target"]).StandardOutput.Trim();
        var current = RunGitChecked(
            [
                "-C", this.RootPath,
                "commit-tree",
                tree,
                "-p",
                firstCurrent,
                "-p",
                firstTarget,
                "-m",
                "merge current",
            ]).StandardOutput.Trim();
        var target = RunGitChecked(
            [
                "-C", this.RootPath,
                "commit-tree",
                tree,
                "-p",
                firstTarget,
                "-p",
                firstCurrent,
                "-m",
                "merge target",
            ]).StandardOutput.Trim();
        RunGitChecked(["-C", this.RootPath, "update-ref", "refs/heads/main", current, root]);
        RunGitChecked(
            ["-C", this.RootPath, "update-ref", $"refs/heads/{targetBranchName}", target]);
    }

    /// <summary>
    ///     Merges a branch and requires the controlled histories to produce conflicts.
    /// </summary>
    /// <param name="branchName">The branch name to merge. Cannot be <see langword="null" /> or empty.</param>
    internal void MergeExpectingConflict(string branchName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(branchName);
        var output = RunGit(
            ["-C", this.RootPath, "merge", "--no-commit", "--no-ff", branchName]);
        if (output.ExitCode == 0)
        {
            throw new InvalidOperationException(
                "The controlled merge unexpectedly completed without conflicts.");
        }
    }

    /// <summary>
    ///     Creates a linked worktree at the current committed revision.
    /// </summary>
    /// <returns>The full linked-worktree path.</returns>
    internal string CreateLinkedWorktree()
    {
        var worktreePath = Path.Combine(this._temporaryDirectory.DirectoryPath, "linked worktree");
        RunGitChecked(
            ["-C", this.RootPath, "worktree", "add", "--quiet", "-b", "linked-branch", worktreePath]);
        return worktreePath;
    }

    /// <summary>
    ///     Adds and commits a local submodule repository.
    /// </summary>
    /// <returns>The full submodule working-tree path.</returns>
    internal string CreateSubmodule()
    {
        var sourcePath = Path.Combine(this._temporaryDirectory.DirectoryPath, "submodule source");
        RunGitChecked(["init", "--initial-branch=main", sourcePath]);
        ConfigureRepository(sourcePath);
        CommitFixture(sourcePath, "submodule.txt", "submodule fixture content\n", "submodule initial commit");

        const string submoduleDirectoryName = "child module";
        var submodulePath = Path.Combine(this.RootPath, submoduleDirectoryName);
        RunGitChecked(
            [
                "-C", this.RootPath,
                "-c",
                "protocol.file.allow=always",
                "submodule",
                "add",
                "--quiet",
                sourcePath,
                submoduleDirectoryName,
            ]);
        RunGitChecked(["-C", this.RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "add submodule"]);
        return submodulePath;
    }

    /// <summary>
    ///     Runs the installed Git executable directly with the given argument sequence.
    /// </summary>
    /// <param name="arguments">
    ///     The Git argument sequence, excluding the executable name. Cannot be <see langword="null" />.
    /// </param>
    /// <returns>The process exit code and separately captured standard streams.</returns>
    internal static (int ExitCode, string StandardOutput, string StandardError) RunGit(
        IReadOnlyList<string> arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var startInfo = new ProcessStartInfo("git")
        {
            CreateNoWindow = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };

        foreach (var argument in arguments)
        {
            startInfo.ArgumentList.Add(argument);
        }

        startInfo.Environment["GIT_OPTIONAL_LOCKS"] = "0";
        startInfo.Environment["GIT_TERMINAL_PROMPT"] = "0";
        startInfo.Environment["GCM_INTERACTIVE"] = "Never";
        startInfo.Environment["GIT_PAGER"] = "cat";
        startInfo.Environment["PAGER"] = "cat";
        startInfo.Environment["LC_ALL"] = "C";
        startInfo.Environment["LANG"] = "C";

        using var process = Process.Start(startInfo)
            ?? throw new InvalidOperationException("The installed Git executable could not be started.");
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();

        if (!process.WaitForExit((int)ProcessTimeout.TotalMilliseconds))
        {
            process.Kill(entireProcessTree: true);
            process.WaitForExit();
            throw new TimeoutException("The controlled Git fixture command exceeded its allowed time.");
        }

        return (
            process.ExitCode,
            standardOutputTask.GetAwaiter().GetResult(),
            standardErrorTask.GetAwaiter().GetResult());
    }

    /// <summary>
    ///     Deletes the repository fixture and all related worktrees and metadata.
    /// </summary>
    public void Dispose()
    {
        this._temporaryDirectory.Dispose();
    }

    private static void ConfigureRepository(string repositoryPath)
    {
        var hooksPath = Path.Combine(repositoryPath, ".change-lens-empty-hooks");
        Directory.CreateDirectory(hooksPath);
        RunGitChecked(["-C", repositoryPath, "config", "user.name", "ChangeLens Test"]);
        RunGitChecked(["-C", repositoryPath, "config", "user.email", "changelens@example.invalid"]);
        RunGitChecked(["-C", repositoryPath, "config", "commit.gpgSign", "false"]);
        RunGitChecked(["-C", repositoryPath, "config", "core.hooksPath", hooksPath]);
    }

    private static void CommitFixture(
        string repositoryPath,
        string relativePath,
        string content,
        string message)
    {
        File.WriteAllText(Path.Combine(repositoryPath, relativePath), content);
        RunGitChecked(["-C", repositoryPath, "add", "--", relativePath]);
        RunGitChecked(
            ["-C", repositoryPath, "commit", "--quiet", "--no-gpg-sign", "-m", message]);
    }

    private static (int ExitCode, string StandardOutput, string StandardError) RunGitChecked(
        IReadOnlyList<string> arguments)
    {
        var output = RunGit(arguments);
        if (output.ExitCode != 0)
        {
            throw new InvalidOperationException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"Controlled Git fixture command failed with exit code {output.ExitCode}: {output.StandardError}"));
        }

        return output;
    }
}
