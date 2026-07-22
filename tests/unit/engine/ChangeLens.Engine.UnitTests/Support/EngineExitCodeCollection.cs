using Xunit;

namespace ChangeLens.Engine.UnitTests.Support;

/// <summary>
///     Prevents host tests that mutate the process exit code from running in parallel.
/// </summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class EngineExitCodeCollection
{
    /// <summary>
    ///     The xUnit collection name used by process-exit-code tests.
    /// </summary>
    public const string Name = "Engine exit code";
}
