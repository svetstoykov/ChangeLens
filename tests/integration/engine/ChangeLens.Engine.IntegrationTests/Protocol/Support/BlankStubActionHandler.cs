using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides a blank declared action for registration-validation tests.
/// </summary>
internal sealed class BlankStubActionHandler : IActionHandler
{
    /// <inheritdoc />
    public static string Action => " ";

    /// <inheritdoc />
    public Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The registration-validation stub does not process requests.");
}
