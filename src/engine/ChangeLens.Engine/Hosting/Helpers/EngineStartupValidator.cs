using ChangeLens.Core.ClaimChecking.Interfaces;
using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.Curation.Services;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Engine.Hosting.Helpers;

/// <summary>
///     Provides the startup invariants the engine enforces over its composed service descriptors.
/// </summary>
/// <remarks>
///     Validation inspects descriptors and resolves nothing, so it runs before the service provider is built and
///     the provider is never built when an invariant fails. Curator reserve checks run when both option instances
///     are registered, publication options enforce checker and frontier invariants, then action-handler registrations
///     are matched against the approved action list.
/// </remarks>
internal static class EngineStartupValidator
{
    /// <summary>
    ///     Validates every engine startup invariant over the composed service descriptors.
    /// </summary>
    /// <param name="services">The composed service descriptors. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     The curator call limits are not positive, the binder reserves cannot hold the configured call,
    ///     checking is enabled without an adapter or with a non-positive checker limit, the frontier cap is not positive, or
    ///     the approved actions are blank or duplicated, or handler registrations are unkeyed, keyed by a non-string
    ///     or blank value, unapproved, missing, or duplicated.
    /// </exception>
    internal static void Validate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ValidateCuratorConfiguration(services);
        ValidatePublicationConfiguration(services);
        ValidateActionHandlerRegistrations(services);
    }

    /// <summary>
    ///     Validates registered curator and binder option instances against the shared reserve rules.
    /// </summary>
    /// <param name="services">The composed service descriptors.</param>
    /// <remarks>
    ///     Both <see cref="EvidenceBinderOptions" /> and <see cref="CuratorOptions" /> must be present as
    ///     implementation instances. When either is missing, this invariant is skipped so a handler-only
    ///     service collection can still be validated.
    /// </remarks>
    /// <exception cref="InvalidOperationException">
    ///     The curator call limits are not positive, or the binder reserves cannot hold the configured call.
    /// </exception>
    private static void ValidateCuratorConfiguration(IServiceCollection services)
    {
        var binderOptions = FindImplementationInstance<EvidenceBinderOptions>(services);
        var curatorOptions = FindImplementationInstance<CuratorOptions>(services);
        if (binderOptions is null || curatorOptions is null)
        {
            return;
        }

        CuratorConfigurationValidator.Validate(binderOptions, curatorOptions);
    }

    private static T? FindImplementationInstance<T>(IServiceCollection services)
        where T : class =>
        services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(T))?.ImplementationInstance as T;

    /// <summary>Validates publication checker and frontier bounds before the service provider is built.</summary>
    /// <param name="services">The composed service descriptors.</param>
    /// <exception cref="InvalidOperationException">
    ///     Claim checking is enabled without a checker adapter or with a non-positive checker limit, or the frontier entry
    ///     cap is not positive.
    /// </exception>
    private static void ValidatePublicationConfiguration(IServiceCollection services)
    {
        var checkerOptions = FindImplementationInstance<ClaimCheckingOptions>(services);
        if (checkerOptions is { Enabled: true } && !services.Any(descriptor => descriptor.ServiceType == typeof(IClaimChecker)))
        {
            throw new InvalidOperationException(
                "Publication configuration enables claim checking, but no IClaimChecker adapter is registered.");
        }

        if (checkerOptions is { Enabled: true }
            && (checkerOptions.MaximumClaims <= 0 || checkerOptions.MaximumPayloadCharacters <= 0 || checkerOptions.Attempts <= 0
                || checkerOptions.MaximumOutputTokens <= 0))
        {
            throw new InvalidOperationException(
                "Publication configuration enables claim checking, but the checker claim count, payload characters, attempts, and "
                + "output tokens must all be positive.");
        }

        var frontierOptions = FindImplementationInstance<EvidenceFrontierOptions>(services);
        if (frontierOptions is not null && frontierOptions.MaximumEntries <= 0)
        {
            throw new InvalidOperationException(
                $"Publication configuration requires a positive evidence frontier maximum entry count; received {frontierOptions.MaximumEntries}.");
        }
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
