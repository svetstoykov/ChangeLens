using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.IntegrationTests.Protocol.Support;
using ChangeLens.Engine.Protocol.Constants;
using ChangeLens.Engine.Protocol.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

/// <summary>
///     Verifies the exact approved action-handler registration invariant.
/// </summary>
public sealed class EngineActionHandlerRegistrationTests
{
    /// <summary>
    ///     Verifies one keyed handler registration per approved action is accepted.
    /// </summary>
    [Fact]
    public void ExactApprovedRegistrationsPassValidation()
    {
        var services = CreateExactRegistrations();

        EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services);
    }

    /// <summary>
    ///     Verifies a blank handler key is rejected and its registration is named.
    /// </summary>
    [Fact]
    public void BlankHandlerKeyIsRejected()
    {
        var services = CreateExactRegistrations();
        services.AddKeyedScoped(typeof(IActionHandler), BlankStubActionHandler.Action, typeof(BlankStubActionHandler));

        var exception = Assert.Throws<InvalidOperationException>(
            () => EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services));

        Assert.Contains(typeof(BlankStubActionHandler).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies duplicate handler keys are rejected and the duplicated action is named.
    /// </summary>
    [Fact]
    public void DuplicateHandlerKeyIsRejected()
    {
        var services = CreateExactRegistrations();
        var duplicatedAction = EngineActionConstants.ApprovedActions[0];
        services.AddKeyedScoped(
            typeof(IActionHandler),
            duplicatedAction,
            typeof(RepositoryOpenStubActionHandler));

        var exception = Assert.Throws<InvalidOperationException>(
            () => EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services));

        Assert.Contains(duplicatedAction, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies a missing approved key is rejected and the absent action is named.
    /// </summary>
    [Fact]
    public void MissingApprovedHandlerKeyIsRejected()
    {
        var missingAction = EngineActionConstants.ApprovedActions[0];
        var services = CreateRegistrations(
            EngineActionConstants.ApprovedActions.Where(action => action != missingAction));

        var exception = Assert.Throws<InvalidOperationException>(
            () => EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services));

        Assert.Contains(missingAction, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies an unapproved handler key is rejected and the extra action is named.
    /// </summary>
    [Fact]
    public void UnapprovedHandlerKeyIsRejected()
    {
        var services = CreateExactRegistrations();
        services.AddKeyedScoped(
            typeof(IActionHandler),
            UnapprovedStubActionHandler.Action,
            typeof(UnapprovedStubActionHandler));

        var exception = Assert.Throws<InvalidOperationException>(
            () => EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services));

        Assert.Contains(UnapprovedStubActionHandler.Action, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies a non-string handler key is rejected and its key type is named.
    /// </summary>
    [Fact]
    public void NonStringHandlerKeyIsRejected()
    {
        var services = CreateExactRegistrations();
        services.AddKeyedScoped(typeof(IActionHandler), 42, typeof(RepositoryOpenStubActionHandler));

        var exception = Assert.Throws<InvalidOperationException>(
            () => EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services));

        Assert.Contains(typeof(int).FullName!, exception.Message, StringComparison.Ordinal);
    }

    /// <summary>
    ///     Verifies an unkeyed handler is rejected and its registration is named.
    /// </summary>
    [Fact]
    public void UnkeyedHandlerIsRejected()
    {
        var services = CreateExactRegistrations();
        services.AddScoped(typeof(IActionHandler), typeof(BlankStubActionHandler));

        var exception = Assert.Throws<InvalidOperationException>(
            () => EngineHostApplicationBuilderExtensions.ValidateActionHandlerRegistrations(services));

        Assert.Contains(typeof(BlankStubActionHandler).FullName!, exception.Message, StringComparison.Ordinal);
    }

    private static ServiceCollection CreateExactRegistrations() =>
        CreateRegistrations(EngineActionConstants.ApprovedActions);

    private static ServiceCollection CreateRegistrations(IEnumerable<string> actions)
    {
        var services = new ServiceCollection();
        foreach (var action in actions)
        {
            services.AddKeyedScoped(
                typeof(IActionHandler),
                action,
                typeof(RepositoryOpenStubActionHandler));
        }

        return services;
    }
}
