using ChangeLens.Engine.Comparisons.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using ChangeLens.Engine.Protocol.Models;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides controlled behavior for the comparison-freshness action.
/// </summary>
/// <param name="handleAsync">The behavior to invoke for a request.</param>
internal sealed class ComparisonCheckFreshnessStubActionHandler(
    Func<EngineProtocolRequest, CancellationToken, Task<ProtocolResponse>> handleAsync) : IActionHandler
{
    /// <inheritdoc />
    public static string Action => ComparisonActionConstants.CheckFreshnessAction;

    /// <inheritdoc />
    public Task<ProtocolResponse> HandleAsync(EngineProtocolRequest request, CancellationToken cancellationToken) =>
        handleAsync(request, cancellationToken);
}
