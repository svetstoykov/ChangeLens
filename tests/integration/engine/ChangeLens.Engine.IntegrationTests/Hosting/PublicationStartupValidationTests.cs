using ChangeLens.Core.ClaimChecking.Models;
using ChangeLens.Core.EvidenceFrontier.Models;
using ChangeLens.Engine.Hosting.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

/// <summary>Verifies publication configuration invariants that must fail before provider construction.</summary>
public sealed class PublicationStartupValidationTests
{
    /// <summary>Verifies enabled checking requires a registered checker port.</summary>
    [Fact]
    public void EnabledCheckerWithoutAdapterIsRejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ClaimCheckingOptions { Enabled = true });
        services.AddSingleton(new EvidenceFrontierOptions());

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(services));

        Assert.Contains("IClaimChecker", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a non-positive frontier cap is rejected.</summary>
    [Fact]
    public void NonPositiveFrontierCapIsRejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ClaimCheckingOptions());
        services.AddSingleton(new EvidenceFrontierOptions { MaximumEntries = 0 });

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(services));

        Assert.Contains("positive evidence frontier maximum", exception.Message, StringComparison.Ordinal);
    }
}
