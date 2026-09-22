using System.Diagnostics;
using ChangeLens.Engine.IntegrationTests.Support;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Represents a controlled Git repository used by real-process protocol tests.
/// </summary>
internal sealed class ProtocolTemporaryGitRepository : IDisposable
{
    private readonly TemporaryDirectory _directory = new();

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProtocolTemporaryGitRepository" /> class.
    /// </summary>
    public ProtocolTemporaryGitRepository()
    {
        this.RunGit(["init", "--initial-branch=main", this.Path]);
        this.RunGit(["-C", this.Path, "config", "user.name", "ChangeLens Test"]);
        this.RunGit(["-C", this.Path, "config", "user.email", "changelens@example.invalid"]);
    }

    /// <summary>Gets the repository path.</summary>
    public string Path => this._directory.DirectoryPath;

    /// <summary>Gets the comparison target created with each commit.</summary>
    public string DefaultTarget => "refs/heads/analysis-target";

    /// <summary>Commits one file and points the controlled comparison target at the new revision.</summary>
    /// <param name="relativePath">The repository-relative file path.</param>
    /// <param name="content">The file content.</param>
    public void CommitFile(string relativePath, string content)
    {
        var filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(this.Path, relativePath));
        Assert.StartsWith(this.Path + System.IO.Path.DirectorySeparatorChar, filePath, StringComparison.Ordinal);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
        this.RunGit(["-C", this.Path, "add", "--", relativePath]);
        this.RunGit(["-C", this.Path, "commit", "--quiet", "--no-gpg-sign", "-m", "protocol fixture"]);
        this.RunGit(["-C", this.Path, "branch", "--force", "analysis-target", "HEAD"]);
    }

    /// <summary>Commits one file at HEAD without moving the comparison target.</summary>
    /// <param name="relativePath">The repository-relative file path.</param>
    /// <param name="content">The file content.</param>
    public void CommitFileAtHead(string relativePath, string content)
    {
        this.WriteTextFile(relativePath, content);
        this.RunGit(["-C", this.Path, "add", "--", relativePath]);
        this.RunGit(["-C", this.Path, "commit", "--quiet", "--no-gpg-sign", "-m", "protocol fixture"]);
    }

    /// <summary>Commits a binary file at HEAD without moving the comparison target.</summary>
    /// <param name="relativePath">The repository-relative file path.</param>
    /// <param name="content">The raw file bytes.</param>
    public void CommitBinaryFileAtHead(string relativePath, byte[] content)
    {
        var filePath = this.Resolve(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
        File.WriteAllBytes(filePath, content);
        this.RunGit(["-C", this.Path, "add", "--", relativePath]);
        this.RunGit(["-C", this.Path, "commit", "--quiet", "--no-gpg-sign", "-m", "protocol fixture"]);
    }

    /// <summary>Renames one tracked file and commits the rename at HEAD without moving the comparison target.</summary>
    /// <param name="fromRelativePath">The existing repository-relative file path.</param>
    /// <param name="toRelativePath">The new repository-relative file path.</param>
    public void CommitRenameAtHead(string fromRelativePath, string toRelativePath)
    {
        this.RunGit(["-C", this.Path, "mv", "--", fromRelativePath, toRelativePath]);
        this.RunGit(["-C", this.Path, "commit", "--quiet", "--no-gpg-sign", "-m", "protocol fixture"]);
    }

    /// <summary>Writes a text file without staging or committing it.</summary>
    /// <param name="relativePath">The repository-relative file path.</param>
    /// <param name="content">The file content.</param>
    public void WriteTextFile(string relativePath, string content)
    {
        var filePath = this.Resolve(relativePath);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(filePath)!);
        File.WriteAllText(filePath, content);
    }

    /// <inheritdoc />
    public void Dispose() => this._directory.Dispose();

    private string Resolve(string relativePath)
    {
        var filePath = System.IO.Path.GetFullPath(System.IO.Path.Combine(this.Path, relativePath));
        Assert.StartsWith(this.Path + System.IO.Path.DirectorySeparatorChar, filePath, StringComparison.Ordinal);
        return filePath;
    }

    private void RunGit(IReadOnlyList<string> arguments)
    {
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

        using var process = Process.Start(startInfo) ?? throw new InvalidOperationException("Git did not start.");
        var standardOutput = process.StandardOutput.ReadToEnd();
        var standardError = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(
            process.ExitCode == 0,
            $"Git exited with {process.ExitCode}. Standard output: {standardOutput}. Standard error: {standardError}.");
    }
}
