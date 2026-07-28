using ChangeLens.Core.Diagnostics.Services;
using ChangeLens.Core.Repositories.Models;
using Xunit;

namespace ChangeLens.Infrastructure.IntegrationTests.Diagnostics;

/// <summary>
///     Verifies that <see cref="PathSanitizer" /> never surfaces a raw home-directory or out-of-repository path.
/// </summary>
public sealed class PathSanitizerTests
{
    private static readonly RepositoryDescriptor Repository = new(
        "acme-corp-payroll",
        Path.Combine(Path.GetTempPath(), "repositories", "acme-corp-payroll"),
        new DetachedRepositoryHead(new string('a', 40)));

    /// <summary>
    ///     Returns the repository display name alone for the repository root itself.
    /// </summary>
    [Fact]
    public void ToRepositoryRelativePathRootReturnsDisplayName()
    {
        var sanitizer = new PathSanitizer();

        var result = sanitizer.ToRepositoryRelativePath(Repository.CanonicalPath, Repository);

        Assert.Equal(Repository.Name, result);
    }

    /// <summary>
    ///     Returns the repository display name plus a forward-slash relative path for a nested file.
    /// </summary>
    [Fact]
    public void ToRepositoryRelativePathNestedFileReturnsDisplayNameAndRelativePath()
    {
        var sanitizer = new PathSanitizer();
        var absolutePath = Path.Combine(Repository.CanonicalPath, "src", "Payroll.cs");

        var result = sanitizer.ToRepositoryRelativePath(absolutePath, Repository);

        Assert.Equal("acme-corp-payroll/src/Payroll.cs", result);
    }

    /// <summary>
    ///     Returns a fixed placeholder, never the raw path, for a path outside the repository root.
    /// </summary>
    [Fact]
    public void ToRepositoryRelativePathOutsideRepositoryReturnsPlaceholder()
    {
        var sanitizer = new PathSanitizer();
        var absolutePath = Path.Combine(Path.GetTempPath(), "repositories", "other-repo", "file.txt");

        var result = sanitizer.ToRepositoryRelativePath(absolutePath, Repository);

        Assert.DoesNotContain(Repository.CanonicalPath, result, StringComparison.Ordinal);
        Assert.DoesNotContain("other-repo", result, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Returns a fixed placeholder for a sibling directory whose name merely starts with the repository path.
    /// </summary>
    [Fact]
    public void ToRepositoryRelativePathSiblingSharingPathPrefixReturnsPlaceholder()
    {
        var sanitizer = new PathSanitizer();
        var absolutePath = Path.Combine($"{Repository.CanonicalPath}-archive", "file.txt");

        var result = sanitizer.ToRepositoryRelativePath(absolutePath, Repository);

        Assert.Equal("<path outside repository>", result);
    }

    /// <summary>
    ///     Treats a file whose name begins with two dots as inside the repository rather than as traversal.
    /// </summary>
    [Fact]
    public void ToRepositoryRelativePathDotPrefixedFileNameStaysInsideRepository()
    {
        var sanitizer = new PathSanitizer();
        var absolutePath = Path.Combine(Repository.CanonicalPath, "..config");

        var result = sanitizer.ToRepositoryRelativePath(absolutePath, Repository);

        Assert.Equal("acme-corp-payroll/..config", result);
    }

    /// <summary>
    ///     Replaces the current user's home-directory prefix with a fixed marker for a nested path.
    /// </summary>
    [Fact]
    public void RedactHomeDirectoryNestedPathReplacesHomePrefix()
    {
        var sanitizer = new PathSanitizer();
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var absolutePath = Path.Combine(homeDirectory, "Projects", "acme-corp-payroll");

        var result = sanitizer.RedactHomeDirectory(absolutePath);

        Assert.Equal("~/Projects/acme-corp-payroll", result);
    }

    /// <summary>
    ///     Returns the fixed marker alone when the path is exactly the home directory.
    /// </summary>
    [Fact]
    public void RedactHomeDirectoryHomeDirectoryItselfReturnsMarker()
    {
        var sanitizer = new PathSanitizer();
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var result = sanitizer.RedactHomeDirectory(homeDirectory);

        Assert.Equal("~", result);
    }

    /// <summary>
    ///     Returns the path unchanged when it does not fall under the current user's home directory.
    /// </summary>
    [Fact]
    public void RedactHomeDirectoryOutsideHomeDirectoryReturnsUnchanged()
    {
        var sanitizer = new PathSanitizer();
        var absolutePath = Path.Combine(Path.GetTempPath(), "repositories", "acme-corp-payroll");

        var result = sanitizer.RedactHomeDirectory(absolutePath);

        Assert.Equal(absolutePath, result);
    }

    /// <summary>
    ///     Leaves a sibling directory whose name merely starts with the home-directory path unchanged.
    /// </summary>
    [Fact]
    public void RedactHomeDirectorySiblingSharingPathPrefixReturnsUnchanged()
    {
        var sanitizer = new PathSanitizer();
        var homeDirectory = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var absolutePath = Path.Combine($"{homeDirectory}-shared", "Projects", "acme-corp-payroll");

        var result = sanitizer.RedactHomeDirectory(absolutePath);

        Assert.Equal(absolutePath, result);
    }

    /// <summary>
    ///     Rejects an empty path rather than treating it as the current working directory.
    /// </summary>
    [Fact]
    public void EmptyAbsolutePathThrowsArgumentException()
    {
        var sanitizer = new PathSanitizer();

        Assert.Throws<ArgumentException>(() => sanitizer.ToRepositoryRelativePath(string.Empty, Repository));
        Assert.Throws<ArgumentException>(() => sanitizer.RedactHomeDirectory(string.Empty));
    }
}
