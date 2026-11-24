using ChangeLens.Core.Analysis.ChangeAnatomy.Helpers;
using ChangeLens.Core.Analysis.ChangeAnatomy.Models;
using ChangeLens.Core.Analysis.ClaimChecking.Helpers;
using ChangeLens.Core.Analysis.ClaimChecking.Interfaces;
using ChangeLens.Core.Analysis.ClaimChecking.Models;
using ChangeLens.Core.Analysis.ContextPolicy.Helpers;
using ChangeLens.Core.Analysis.ContextPolicy.Models;
using ChangeLens.Core.Analysis.Correspondence.Helpers;
using ChangeLens.Core.Analysis.Correspondence.Models;
using ChangeLens.Core.Analysis.Curation.Helpers;
using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.EvidenceBinder.Helpers;
using ChangeLens.Core.Analysis.EvidenceBinder.Models;
using ChangeLens.Core.Analysis.EvidenceFrontier.Helpers;
using ChangeLens.Core.Analysis.EvidenceFrontier.Models;
using ChangeLens.Core.Analysis.EvidenceGraph.Helpers;
using ChangeLens.Core.Analysis.EvidenceGraph.Models;
using ChangeLens.Core.Analysis.Review.Helpers;
using ChangeLens.Core.Analysis.Review.Models;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace ChangeLens.Engine.Hosting.Helpers;

/// <summary>
///     Provides the startup invariants the engine enforces over its composed service descriptors.
/// </summary>
/// <remarks>
///     Validation inspects descriptors and resolves nothing, so it runs before the service provider is built and
///     the provider is never built when an invariant fails. Core option validators run first for every registered
///     Core option instance, followed by Engine-boundary checks for claim-checker adapter registration and approved
///     action-handler registrations.
/// </remarks>
internal static class EngineStartupValidator
{
    /// <summary>
    ///     Validates every engine startup invariant over the composed service descriptors.
    /// </summary>
    /// <param name="services">The composed service descriptors. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="services" /> is <see langword="null" />.</exception>
    /// <exception cref="InvalidOperationException">
    ///     Any registered Core options instance violates its validator, claim checking is enabled without an
    ///     <see cref="IClaimChecker" /> adapter, the approved actions are blank or duplicated, or handler registrations are
    ///     unkeyed, keyed by a non-string or blank value, unapproved, missing, or duplicated.
    /// </exception>
    internal static void Validate(IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        ValidateCoreOptions(services);
        ValidateClaimCheckerAdapterRegistration(services);
        ValidateActionHandlerRegistrations(services);
    }

    /// <summary>
    ///     Validates every registered Core option instance through its Core validator.
    /// </summary>
    /// <param name="services">The composed service descriptors.</param>
    /// <exception cref="InvalidOperationException">
    ///     Any registered Core option instance violates its bounds, weights, reserves, or limits.
    /// </exception>
    private static void ValidateCoreOptions(IServiceCollection services)
    {
        var anatomyOptions = FindImplementationInstance<ChangeAnatomyOptions>(services);
        if (anatomyOptions is not null)
        {
            ChangeAnatomyOptionsValidator.Validate(anatomyOptions);
        }

        var correspondenceOptions = FindImplementationInstance<CorrespondenceOptions>(services);
        if (correspondenceOptions is not null)
        {
            if (anatomyOptions is not null)
            {
                CorrespondenceOptionsValidator.Validate(correspondenceOptions, anatomyOptions);
            }
            else
            {
                CorrespondenceOptionsValidator.Validate(correspondenceOptions);
            }
        }

        var graphOptions = FindImplementationInstance<EvidenceGraphOptions>(services);
        if (graphOptions is not null)
        {
            EvidenceGraphOptionsValidator.Validate(graphOptions);
        }

        var policyOptions = FindImplementationInstance<ContextPolicyOptions>(services);
        if (policyOptions is not null)
        {
            if (graphOptions is not null)
            {
                ContextPolicyOptionsValidator.Validate(graphOptions, policyOptions);
            }
            else
            {
                ContextPolicyOptionsValidator.Validate(policyOptions);
            }
        }

        var binderOptions = FindImplementationInstance<EvidenceBinderOptions>(services);
        if (binderOptions is not null)
        {
            EvidenceBinderOptionsValidator.Validate(binderOptions);
        }

        var frontierOptions = FindImplementationInstance<EvidenceFrontierOptions>(services);
        if (frontierOptions is not null)
        {
            EvidenceFrontierOptionsValidator.Validate(frontierOptions);
        }

        var curatorOptions = FindImplementationInstance<CuratorOptions>(services);
        if (binderOptions is not null && curatorOptions is not null)
        {
            CuratorOptionsValidator.Validate(binderOptions, curatorOptions);
        }

        var reviewerOptions = FindImplementationInstance<ReviewerOptions>(services);
        if (binderOptions is not null && reviewerOptions is not null)
        {
            ReviewerOptionsValidator.Validate(binderOptions, reviewerOptions);
        }

        var checkerOptions = FindImplementationInstance<ClaimCheckingOptions>(services);
        if (checkerOptions is not null)
        {
            ClaimCheckingOptionsValidator.Validate(checkerOptions);
        }
    }

    /// <summary>Validates that enabled claim checking has a registered checker adapter.</summary>
    /// <param name="services">The composed service descriptors.</param>
    /// <exception cref="InvalidOperationException">Claim checking is enabled without a registered checker adapter.</exception>
    private static void ValidateClaimCheckerAdapterRegistration(IServiceCollection services)
    {
        var checkerOptions = FindImplementationInstance<ClaimCheckingOptions>(services);
        if (checkerOptions is { Enabled: true } && !services.Any(descriptor => descriptor.ServiceType == typeof(IClaimChecker)))
        {
            throw new InvalidOperationException(
                "Publication configuration enables claim checking, but no IClaimChecker adapter is registered.");
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

    private static T? FindImplementationInstance<T>(IServiceCollection services)
        where T : class =>
        services.FirstOrDefault(descriptor => descriptor.ServiceType == typeof(T))?.ImplementationInstance as T;

    private static string DescribeActionHandlerRegistration(ServiceDescriptor descriptor)
    {
        var implementationType = descriptor.IsKeyedService
            ? descriptor.KeyedImplementationType
            : descriptor.ImplementationType;
        return $"'{implementationType?.FullName ?? "<factory or instance>"}'";
    }
}
