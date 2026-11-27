using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Groups the test classes that start a real engine process so that their tests run one at a time.
/// </summary>
[CollectionDefinition]
public sealed class EngineProcessCollection;
