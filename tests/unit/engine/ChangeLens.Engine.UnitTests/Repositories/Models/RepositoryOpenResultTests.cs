using System.Text.Json;
using ChangeLens.Core.Repositories.Models;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Protocol.Services;
using ChangeLens.Engine.Repositories.Models;
using Xunit;

namespace ChangeLens.Engine.UnitTests.Repositories.Models;

/// <summary>
///     Verifies repository descriptor mapping and shared result serialization.
/// </summary>
public sealed class RepositoryOpenResultTests
{
    private const string Revision = "0123456789abcdef0123456789abcdef01234567";
    private readonly EngineProtocolSerializer _serializer = new();

    /// <summary>
    ///     Verifies that an attached Core descriptor matches the shared branch-result fixture.
    /// </summary>
    [Fact]
    public void BranchDescriptorMatchesSharedResultFixture()
    {
        var descriptor = new RepositoryDescriptor(
            "change_lens",
            "/projects/change_lens",
            new BranchRepositoryHead("main", Revision));

        AssertMatchesFixture(descriptor, "desktop-42", "repositories-open.branch.result.json");
    }

    /// <summary>
    ///     Verifies that a detached Core descriptor matches the shared detached-result fixture.
    /// </summary>
    [Fact]
    public void DetachedDescriptorMatchesSharedResultFixture()
    {
        var descriptor = new RepositoryDescriptor(
            "change_lens",
            "/projects/change_lens",
            new DetachedRepositoryHead(Revision));

        AssertMatchesFixture(descriptor, "desktop-43", "repositories-open.detached.result.json");
    }

    private void AssertMatchesFixture(
        RepositoryDescriptor descriptor,
        string requestId,
        string fixtureFileName)
    {
        var response = ProtocolResponseFactory.FromResult(
            requestId,
            Result.Success(RepositoryOpenResult.FromDescriptor(descriptor)));
        var serialized = _serializer.SerializeResponse(response);
        Assert.True(serialized.IsSuccess);
        using var actual = JsonDocument.Parse(serialized.Data!);
        using var expected = JsonDocument.Parse(
            File.ReadAllText(Path.Combine(
                FindRepositoryRoot(),
                "contracts",
                "engine-protocol",
                "v1",
                "fixtures",
                fixtureFileName)));

        Assert.True(JsonElement.DeepEquals(expected.RootElement, actual.RootElement));
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "src", "engine", "ChangeLens.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("The ChangeLens repository root could not be located.");
    }
}
