using ChangeLens.Core.Git.Services;
using ChangeLens.Core.Repositories.Constants;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Infrastructure.FileSystem.Services;
using ChangeLens.Infrastructure.Git.Services;
using ChangeLens.Infrastructure.IntegrationTests.Git.Support;
using ChangeLens.Infrastructure.IntegrationTests.Support;
using Xunit;
using Xunit.Sdk;

namespace ChangeLens.Infrastructure.IntegrationTests.Git.Services;

/// <summary>
///     Verifies real Git repository inspection and its read-only state contract.
/// </summary>
public sealed class GitRepositoryInspectorIntegrationTests
{
    /// <summary>
    ///     Asynchronously inspects a normal branch without changing repository content or status.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_NormalBranch_ReturnsBranchAndPreservesState()
    {
        using var repository = new TemporaryGitRepository();

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        Assert.True(result.IsSuccess);
        var descriptor = Assert.IsType<RepositoryDescriptor>(result.Data);
        var head = Assert.IsType<BranchRepositoryHead>(descriptor.Head);
        Assert.Equal("main", head.Name);
        Assert.Equal(repository.Revision, head.Revision);
        Assert.Equal(await ResolveAsync(repository.RootPath), descriptor.CanonicalPath);
    }

    /// <summary>
    ///     Asynchronously inspects detached HEAD without changing repository content or status.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_DetachedHead_ReturnsDetachedRevisionAndPreservesState()
    {
        using var repository = new TemporaryGitRepository();
        repository.CheckoutDetached();

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        Assert.True(result.IsSuccess);
        var descriptor = Assert.IsType<RepositoryDescriptor>(result.Data);
        var head = Assert.IsType<DetachedRepositoryHead>(descriptor.Head);
        Assert.Equal(repository.Revision, head.Revision);
    }

    /// <summary>
    ///     Asynchronously inspects a repository whose path contains spaces and Unicode characters.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_SpacesAndUnicodePath_ReturnsCanonicalRepository()
    {
        using var repository = new TemporaryGitRepository("repository with spaces Ж");

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        Assert.True(result.IsSuccess);
        var descriptor = Assert.IsType<RepositoryDescriptor>(result.Data);
        Assert.Equal("repository with spaces Ж", descriptor.Name);
        Assert.Equal(await ResolveAsync(repository.RootPath), descriptor.CanonicalPath);
    }

    /// <summary>
    ///     Asynchronously resolves a nested selection to its repository top level without changing state.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_NestedDirectory_ReturnsRepositoryTopLevelAndPreservesState()
    {
        using var repository = new TemporaryGitRepository();
        var nestedPath = repository.CreateNestedDirectory();

        var result = await InspectWithoutMutationAsync(nestedPath);

        Assert.True(result.IsSuccess);
        var descriptor = Assert.IsType<RepositoryDescriptor>(result.Data);
        Assert.Equal(await ResolveAsync(repository.RootPath), descriptor.CanonicalPath);
    }

    /// <summary>
    ///     Asynchronously treats a linked worktree as its own repository root without changing shared metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_LinkedWorktree_ReturnsWorktreeRootAndPreservesSharedState()
    {
        using var repository = new TemporaryGitRepository();
        var worktreePath = repository.CreateLinkedWorktree();

        var result = await InspectWithoutMutationAsync(worktreePath);

        Assert.True(result.IsSuccess);
        var descriptor = Assert.IsType<RepositoryDescriptor>(result.Data);
        var head = Assert.IsType<BranchRepositoryHead>(descriptor.Head);
        Assert.Equal("linked-branch", head.Name);
        Assert.Equal(await ResolveAsync(worktreePath), descriptor.CanonicalPath);
    }

    /// <summary>
    ///     Asynchronously treats a checked-out submodule as its own repository without changing shared metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_Submodule_ReturnsSubmoduleRepositoryAndPreservesSharedState()
    {
        using var repository = new TemporaryGitRepository();
        var submodulePath = repository.CreateSubmodule();

        var result = await InspectWithoutMutationAsync(submodulePath);

        Assert.True(result.IsSuccess);
        var descriptor = Assert.IsType<RepositoryDescriptor>(result.Data);
        Assert.Equal("child module", descriptor.Name);
        Assert.Equal(await ResolveAsync(submodulePath), descriptor.CanonicalPath);
    }

    /// <summary>
    ///     Asynchronously returns the stable non-repository error without changing selected-directory content.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_NonRepository_ReturnsNotGitRepositoryAndPreservesState()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        await File.WriteAllTextAsync(
            Path.Combine(temporaryDirectory.DirectoryPath, "content.txt"),
            "not a repository",
            TestContext.Current.CancellationToken);

        var result = await InspectWithoutMutationAsync(temporaryDirectory.DirectoryPath);

        AssertFailure(
            result,
            ErrorType.UnprocessableInput,
            RepositoryErrorCode.NotGitRepository);
    }

    /// <summary>
    ///     Asynchronously returns the stable worktree-unavailable error without changing bare metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_BareRepository_ReturnsWorkTreeUnavailableAndPreservesState()
    {
        using var repository = TemporaryGitRepository.CreateBare();

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        AssertFailure(
            result,
            ErrorType.UnprocessableInput,
            RepositoryErrorCode.WorkTreeUnavailable);
    }

    /// <summary>
    ///     Asynchronously returns the stable HEAD-unavailable error without changing unborn metadata.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_UnbornRepository_ReturnsHeadUnavailableAndPreservesState()
    {
        using var repository = TemporaryGitRepository.CreateUnborn();

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        AssertFailure(
            result,
            ErrorType.UnprocessableInput,
            RepositoryErrorCode.HeadUnavailable);
    }

    /// <summary>
    ///     Asynchronously returns a full SHA-1 revision without changing repository state.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_Sha1Repository_ReturnsFortyCharacterRevisionAndPreservesState()
    {
        using var repository = new TemporaryGitRepository();

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(40, Assert.IsType<RepositoryDescriptor>(result.Data).Head.Revision.Length);
    }

    /// <summary>
    ///     Asynchronously returns a full SHA-256 revision without changing repository state when Git supports it.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_Sha256Repository_ReturnsSixtyFourCharacterRevisionAndPreservesState()
    {
        if (!TemporaryGitRepository.SupportsObjectFormat("sha256"))
        {
            throw SkipException.ForSkip("The installed Git executable does not support SHA-256 repositories.");
        }

        using var repository = new TemporaryGitRepository("sha256-repository", "sha256");

        var result = await InspectWithoutMutationAsync(repository.RootPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(64, Assert.IsType<RepositoryDescriptor>(result.Data).Head.Revision.Length);
    }

    /// <summary>
    ///     Asynchronously normalizes a supported directory link without changing the target repository.
    /// </summary>
    /// <returns>A task that represents the asynchronous operation.</returns>
    [Fact]
    public async Task InspectAsync_DirectoryLink_ReturnsPhysicalRepositoryAndPreservesState()
    {
        using var repository = new TemporaryGitRepository();
        var parentPath = Directory.GetParent(repository.RootPath)?.FullName
            ?? throw new InvalidOperationException("The fixture repository does not have a parent directory.");
        var linkPath = Path.Combine(parentPath, "repository-link");
        CreateDirectoryLinkOrSkip(linkPath, repository.RootPath);

        var result = await InspectWithoutMutationAsync(linkPath, repository.RootPath);

        Assert.True(result.IsSuccess);
        Assert.Equal(
            await ResolveAsync(repository.RootPath),
            Assert.IsType<RepositoryDescriptor>(result.Data).CanonicalPath);
    }

    private static GitRepositoryInspector CreateInspector() =>
        new(new GitCliCommandRunner(), new PhysicalRepositoryPathResolver());

    private static async Task<Result<RepositoryDescriptor>> InspectWithoutMutationAsync(
        string selectedPath,
        string? snapshotPath = null)
    {
        var capturedPath = snapshotPath ?? selectedPath;
        var before = RepositoryStateSnapshot.Capture(capturedPath);

        var result = await CreateInspector().InspectAsync(
            selectedPath,
            TestContext.Current.CancellationToken);

        var after = RepositoryStateSnapshot.Capture(capturedPath);
        Assert.Equal(before.PorcelainStatus, after.PorcelainStatus);
        Assert.Equal(before.FileHashes, after.FileHashes);
        return result;
    }

    private static async Task<string> ResolveAsync(string path)
    {
        var result = await new PhysicalRepositoryPathResolver().ResolveAsync(
            path,
            TestContext.Current.CancellationToken);
        Assert.True(result.IsSuccess);
        return Assert.IsType<string>(result.Data);
    }

    private static void AssertFailure(
        Result<RepositoryDescriptor> result,
        ErrorType expectedType,
        string expectedCode)
    {
        Assert.True(result.IsFailure);
        var error = Assert.Single(result.Errors);
        Assert.Equal(expectedType, error.Type);
        Assert.Equal(expectedCode, error.Code);
    }

    private static void CreateDirectoryLinkOrSkip(
        string linkPath,
        string targetPath)
    {
        try
        {
            Directory.CreateSymbolicLink(linkPath, targetPath);
        }
        catch (Exception exception) when (
            exception is IOException
                or UnauthorizedAccessException
                or PlatformNotSupportedException)
        {
            throw SkipException.ForSkip(
                $"The operating system refused the directory-link fixture: {exception.Message}");
        }
    }
}
