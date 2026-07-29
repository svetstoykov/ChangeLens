using ChangeLens.Core.Results.Models;
using ChangeLens.Core.Comparisons.Models;
using ChangeLens.Engine.Comparisons.Models;

namespace ChangeLens.Engine.Comparisons.Interfaces;

/// <summary>
///     Defines construction of protocol-bounded comparison target pages.
/// </summary>
/// <remarks>
///     Implementations are registered as scoped services. They serve one request and do not need to be thread-safe.
/// </remarks>
internal interface IComparisonTargetPageBuilder
{
    /// <summary>
    ///     Builds one deterministic target page after measuring complete correlated response envelopes.
    /// </summary>
    /// <param name="requestId">The request identifier. Cannot be <see langword="null" />.</param>
    /// <param name="targetSet">The ordered Core target set. Cannot be <see langword="null" />.</param>
    /// <returns>The bounded target page or a protocol serialization failure.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="requestId" /> or <paramref name="targetSet" /> is <see langword="null" />.
    /// </exception>
    Result<ComparisonTargetPageResult> Build(string requestId, ComparisonTargetSet targetSet);
}
