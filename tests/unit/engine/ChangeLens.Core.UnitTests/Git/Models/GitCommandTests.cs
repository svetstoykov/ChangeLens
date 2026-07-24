using ChangeLens.Core.Git.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Results.Models;
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

        var command = CreateCommand(arguments);
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
        var command = CreateCommand(["--version"]);
        var arguments = Assert.IsAssignableFrom<IList<string>>(command.Arguments);

        Assert.Throws<NotSupportedException>(() => arguments.Add("status"));
    }

    /// <summary>
    ///     Verifies that command construction preserves positive execution limits.
    /// </summary>
    [Fact]
    public void ConstructorPreservesIndependentLimitsAndPolicyErrors()
    {
        var timeoutError = OperationError.Timeout("Timed out.", "comparison.timedOut");
        var outputLimitError = OperationError.UnprocessableInput("Too large.", "comparison.tooLarge");
        var inspectionError = OperationError.ExternalDependencyFailure(
            "Inspection failed.",
            "comparison.inspectionFailed");
        var timeout = TimeSpan.FromSeconds(10);

        var command = new GitCommand(
            ["status", "--porcelain=v2", "-z"],
            timeout,
            8 * 1024 * 1024,
            64 * 1024,
            new GitCommandErrorPolicy(timeoutError, outputLimitError, inspectionError));

        Assert.Equal(["status", "--porcelain=v2", "-z"], command.Arguments);
        Assert.Equal(timeout, command.Timeout);
        Assert.Equal(8 * 1024 * 1024, command.MaximumStandardOutputBytes);
        Assert.Equal(64 * 1024, command.MaximumStandardErrorBytes);
        Assert.Same(timeoutError, command.ErrorPolicy.TimedOut);
        Assert.Same(outputLimitError, command.ErrorPolicy.OutputLimitExceeded);
        Assert.Same(inspectionError, command.ErrorPolicy.InspectionFailed);
    }

    /// <summary>
    ///     Verifies that command construction rejects a missing argument collection.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullArguments()
    {
        Assert.Throws<ArgumentNullException>(
            () => CreateCommand(null!));
    }

    /// <summary>
    ///     Verifies that command construction rejects null values inside the argument collection.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullArgument()
    {
        Assert.Throws<ArgumentException>(
            () => CreateCommand(["--version", null!]));
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
            () => new GitCommand(["--version"], TimeSpan.FromTicks(ticks), 1024, 1024, CreatePolicy()));
    }

    /// <summary>
    ///     Verifies that command construction rejects nonpositive stream limits.
    /// </summary>
    /// <param name="maximumStreamBytes">The invalid stream limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonpositiveStandardOutputLimit(int maximumStreamBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCommand(
                ["--version"],
                TimeSpan.FromSeconds(1),
                maximumStreamBytes,
                1024,
                CreatePolicy()));
    }

    /// <summary>
    ///     Verifies that command construction rejects nonpositive standard-error limits.
    /// </summary>
    /// <param name="maximumStreamBytes">The invalid stream limit.</param>
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void ConstructorRejectsNonpositiveStandardErrorLimit(int maximumStreamBytes)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new GitCommand(
                ["--version"],
                TimeSpan.FromSeconds(1),
                1024,
                maximumStreamBytes,
                CreatePolicy()));
    }

    /// <summary>
    ///     Verifies that command construction rejects a missing error policy.
    /// </summary>
    [Fact]
    public void ConstructorRejectsNullErrorPolicy()
    {
        Assert.Throws<ArgumentNullException>(
            () => new GitCommand(["--version"], TimeSpan.FromSeconds(1), 1024, 1024, null!));
    }

    /// <summary>
    ///     Verifies that error policy construction rejects missing terminal errors.
    /// </summary>
    [Fact]
    public void ErrorPolicyConstructorRejectsNullErrors()
    {
        var error = OperationError.Timeout("Timed out.", "comparison.timedOut");

        Assert.Throws<ArgumentNullException>(() => new GitCommandErrorPolicy(null!, error, error));
        Assert.Throws<ArgumentNullException>(() => new GitCommandErrorPolicy(error, null!, error));
        Assert.Throws<ArgumentNullException>(() => new GitCommandErrorPolicy(error, error, null!));
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

    private static GitCommand CreateCommand(IEnumerable<string> arguments) =>
        new(arguments, TimeSpan.FromSeconds(1), 1024, 1024, CreatePolicy());

    private static GitCommandErrorPolicy CreatePolicy() =>
        new(
            OperationError.Timeout("Timed out.", "comparison.timedOut"),
            OperationError.UnprocessableInput("Too large.", "comparison.tooLarge"),
            OperationError.ExternalDependencyFailure("Inspection failed.", "comparison.inspectionFailed"));
}
