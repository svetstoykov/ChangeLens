namespace ChangeLens.Infrastructure.ModelCompletion.Constants;

/// <summary>Defines the OpenRouter upstream orderings the adapter sends.</summary>
internal static class OpenRouterRoutingConstants
{
    /// <summary>The upstream orderings OpenRouter accepts; any other configured value is not sent.</summary>
    internal static readonly string[] Sorts = ["price", "throughput", "latency"];
}
