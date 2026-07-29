using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides an unapproved declared action for registration-validation tests.
/// </summary>
internal sealed class UnapprovedStubActionHandler : IActionHandler
{
    /// <inheritdoc />
    public static string Action => "tests.unapproved";

    /// <inheritdoc />
    public Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        throw new NotSupportedException("The registration-validation stub does not process requests.");
}
