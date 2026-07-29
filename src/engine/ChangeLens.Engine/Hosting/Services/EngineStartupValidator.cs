using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Engine.Hosting.Services;

/// <summary>
///     Provides the startup invariants the engine enforces over its composed service descriptors.
/// </summary>
/// <remarks>
///     Validation inspects descriptors and resolves nothing, so it runs before the service provider is built and
///     the provider is never built when an invariant fails.
/// </remarks>
internal static class EngineStartupValidator
{
    /// <summary>
    ///     Validates every engine startup invariant over the composed service descriptors.
    /// </summary>
    /// <param name="services">The composed service descriptors. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The approved actions are blank or duplicated, or handler registrations are unkeyed, keyed by a non-string
    ///     or blank value, unapproved, missing, or duplicated.
    /// </exception>
    internal static void Validate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ValidateActionHandlerRegistrations(services);
    }

    /// <summary>Validates that action-handler registrations exactly match the approved action list.</summary>
    /// <param name="services">The composed service descriptors.</param>
    /// <exception cref="InvalidOperationException">
    ///     The approved actions are blank or duplicated, or handler registrations are unkeyed, keyed by a non-string
    ///     or blank value, unapproved, missing, or duplicated.
    /// </exception>
    private static void ValidateActionHandlerRegistrations(IServiceCollection services)
    {
        var issues = new List<string>();
        var approvedActions = EngineActionConstants.ApprovedActions;
        var validApprovedActions = approvedActions
            .Where(action => !string.IsNullOrWhiteSpace(action))
            .ToHashSet(StringComparer.Ordinal);

        issues.AddRange(
            approvedActions
                .Select((action, index) => (action, index))
                .Where(item => string.IsNullOrWhiteSpace(item.action))
                .Select(item => $"approved action at index {item.index} is blank"));
        issues.AddRange(
            approvedActions
                .Where(action => !string.IsNullOrWhiteSpace(action))
                .GroupBy(action => action, StringComparer.Ordinal)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => $"approved action '{group.Key}' appears {group.Count()} times"));

        var registrationsByApprovedAction = validApprovedActions.ToDictionary(action => action, _ => 0, StringComparer.Ordinal);
        foreach (var descriptor in services.Where(descriptor => descriptor.ServiceType == typeof(IActionHandler)))
        {
            var registration = DescribeActionHandlerRegistration(descriptor);
            if (!descriptor.IsKeyedService)
            {
                issues.Add($"handler registration {registration} is unkeyed");
                continue;
            }

            if (descriptor.ServiceKey is not string action)
            {
                issues.Add(
                    $"handler registration {registration} has non-string key " +
                    $"'{descriptor.ServiceKey?.GetType().FullName ?? "<null>"}'");
                continue;
            }

            if (string.IsNullOrWhiteSpace(action))
            {
                issues.Add($"handler registration {registration} has a blank action key");
                continue;
            }

            if (!validApprovedActions.Contains(action))
            {
                issues.Add($"handler registration {registration} uses unapproved action '{action}'");
                continue;
            }

            registrationsByApprovedAction[action]++;
        }

        issues.AddRange(
            registrationsByApprovedAction
                .Where(pair => pair.Value == 0)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"approved action '{pair.Key}' has no handler registration"));
        issues.AddRange(
            registrationsByApprovedAction
                .Where(pair => pair.Value > 1)
                .OrderBy(pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => $"approved action '{pair.Key}' has {pair.Value} handler registrations"));

        if (issues.Count > 0)
        {
            throw new InvalidOperationException(
                "Action-handler registration validation failed: " + string.Join("; ", issues) + ".");
        }
    }

    private static string DescribeActionHandlerRegistration(ServiceDescriptor descriptor)
    {
        var implementationType = descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType
            : descriptor.ImplementationType;
        return $"'{implementationType?.FullName ?? "<factory or instance>"}'";
    }
}
