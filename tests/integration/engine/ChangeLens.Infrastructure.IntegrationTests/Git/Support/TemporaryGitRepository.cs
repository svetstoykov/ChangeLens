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
        _temporaryDirectory = new TemporaryDirectory();
        RootPath = Path.Combine(_temporaryDirectory.DirectoryPath, directoryName);

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

        initArguments.Add(RootPath);
        RunGitChecked(initArguments);

        if (bare)
        {
            return;
        }

        ConfigureRepository(RootPath);
        if (createInitialCommit)
        {
            CommitFixture(RootPath, "fixture.txt", "initial fixture content\n", "initial commit");
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
        RunGitChecked(["-C", RootPath, "rev-parse", "--verify", "HEAD"]).StandardOutput.Trim();

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
        Directory.CreateDirectory(Path.Combine(RootPath, "nested", "directory")).FullName;

    /// <summary>
    ///     Detaches HEAD at the current committed revision.
    /// </summary>
    internal void CheckoutDetached()
    {
        RunGitChecked(["-C", RootPath, "checkout", "--detach", "--quiet"]);
    }

    /// <summary>
    ///     Creates a linked worktree at the current committed revision.
    /// </summary>
    /// <returns>The full linked-worktree path.</returns>
    internal string CreateLinkedWorktree()
    {
        var worktreePath = Path.Combine(_temporaryDirectory.DirectoryPath, "linked worktree");
        RunGitChecked(
            ["-C", RootPath, "worktree", "add", "--quiet", "-b", "linked-branch", worktreePath]);
        return worktreePath;
    }

    /// <summary>
    ///     Adds and commits a local submodule repository.
    /// </summary>
    /// <returns>The full submodule working-tree path.</returns>
    internal string CreateSubmodule()
    {
        var sourcePath = Path.Combine(_temporaryDirectory.DirectoryPath, "submodule source");
        RunGitChecked(["init", "--initial-branch=main", sourcePath]);
        ConfigureRepository(sourcePath);
        CommitFixture(sourcePath, "submodule.txt", "submodule fixture content\n", "submodule initial commit");

        const string submoduleDirectoryName = "child module";
        var submodulePath = Path.Combine(RootPath, submoduleDirectoryName);
        RunGitChecked(
            [
                "-C",
                RootPath,
                "-c",
                "protocol.file.allow=always",
                "submodule",
                "add",
                "--quiet",
                sourcePath,
                submoduleDirectoryName,
            ]);
        RunGitChecked(["-C", RootPath, "commit", "--quiet", "--no-gpg-sign", "-m", "add submodule"]);
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
        _temporaryDirectory.Dispose();
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
