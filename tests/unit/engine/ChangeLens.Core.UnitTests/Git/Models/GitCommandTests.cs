using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Constants;
using Xunit;

namespace ChangeLens.Core.UnitTests.Git.Models;

/// <summary>
///     Verifies the immutable Git command boundary contract and stable constants.
/// </summary>
public sealed class GitCommandTests
{
    /// <summary>
    ///     Verifies that command construction copies the supplied arguments.
    /// </summary>
    [Fact]
    public void ConstructorCopiesArguments()
    {
        var arguments = new List<string> { "--version" };

        var command = new GitCommand(arguments, TimeSpan.FromSeconds(1), 1024);
        arguments[0] = "status";
        arguments.Add("--short");

        Assert.Equal(["--version"], command.Arguments);
    }

    /// <summary>
    ///     Verifies that callers cannot mutate the command's exposed arguments.
    /// </summary>
    [Fact]
    public void ArgumentsAreReadOnly()
    {
        var command = new GitCommand(["--version"], TimeSpan.FromSeconds(1), 1024);
        var arguments = Assert.IsAssignableFrom<IList<string>>(command.Arguments);

        Assert.Throws<NotSupportedException>(() => arguments.Add("status"));
    }

    /// <summary>
    ///     Verifies that command construction preserves positive execution limits.
    /// </summary>
    [Fact]
    public void ConstructorPreservesPositiveLimits()
    {
        var timeout = TimeSpan.FromSeconds(3);

        var command = new GitCommand(["--version"], timeout, 4096);

        Assert.Equal(timeout, command.Timeout);
        Assert.Equal(4096, command.MaximumStreamBytes);
    }

    /// <summary>
    ///     Verifies that command construction rejects a missing argument collection.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GitCommand(null!, TimeSpan.FromSeconds(1), 1024));
    }

    /// <summary>
    ///     Verifies that command construction rejects null values inside the argument collection.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullArgument()
    {
        Assert.Throws<ArgumentException>(
            () => new GitCommand(["--version", null!], TimeSpan.FromSeconds(1), 1024));
    }

    /// <summary>
    ///     Verifies that command construction rejects nonpositive timeouts.
    /// </summary>
    /// <param name="ticks">The invalid timeout length in ticks.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonpositiveTimeout(long ticks)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCommand(["--version"], TimeSpan.FromTicks(ticks), 1024));
    }

    /// <summary>
    ///     Verifies that command construction rejects nonpositive stream limits.
    /// </summary>
    /// <param name="maximumStreamBytes">The invalid stream limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonpositiveStreamLimit(int maximumStreamBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCommand(["--version"], TimeSpan.FromSeconds(1), maximumStreamBytes));
    }

    /// <summary>
    ///     Verifies the fixed inspection limits used by Core.
    /// </summary>
    [Fact]
    public void InspectionConstantsMatchContract()
    {
        Assert.Equal(65_536, GitInspectionConstants.MaximumStreamBytes);
        Assert.Equal(TimeSpan.FromSeconds(15), GitInspectionConstants.InspectionTimeout);
    }

    /// <summary>
    ///     Verifies the stable repository and Git error codes.
    /// </summary>
    [Fact]
    public void ErrorCodesMatchContract()
    {
        Assert.Equal("repository.invalidPath", RepositoryErrorCode.InvalidPath);
        Assert.Equal("repository.pathNotFound", RepositoryErrorCode.PathNotFound);
        Assert.Equal("repository.accessDenied", RepositoryErrorCode.AccessDenied);
        Assert.Equal("repository.notGitRepository", RepositoryErrorCode.NotGitRepository);
        Assert.Equal("repository.workTreeUnavailable", RepositoryErrorCode.WorkTreeUnavailable);
        Assert.Equal("repository.headUnavailable", RepositoryErrorCode.HeadUnavailable);
        Assert.Equal("repository.inspectionFailed", RepositoryErrorCode.InspectionFailed);
        Assert.Equal("git.unavailable", GitErrorCode.Unavailable);
        Assert.Equal("git.timedOut", GitErrorCode.TimedOut);
    }
}
