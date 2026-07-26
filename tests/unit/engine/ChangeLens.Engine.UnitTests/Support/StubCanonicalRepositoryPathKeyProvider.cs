using ChangeLens.Core.LocalState.Interfaces;

namespace ChangeLens.Engine.UnitTests.Support;

internal sealed class StubCanonicalRepositoryPathKeyProvider : ICanonicalRepositoryPathKeyProvider
{
    public string CreateKey(string canonicalPath) => canonicalPath;
}
