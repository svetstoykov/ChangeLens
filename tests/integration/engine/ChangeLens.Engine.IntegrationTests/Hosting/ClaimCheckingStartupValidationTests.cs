using ChangeLens.Core.Analysis.ClaimChecking.Constants;
using ChangeLens.Core.Analysis.ClaimChecking.Interfaces;
using ChangeLens.Core.Analysis.ClaimChecking.Models;
using ChangeLens.Core.Analysis.ClaimChecking.Services;
using ChangeLens.Core.Analysis.Publication.Interfaces;
using ChangeLens.Engine.Analysis.Extensions;
using ChangeLens.Engine.AnalysisRuns.Extensions;
using ChangeLens.Engine.Comparisons.Extensions;
using ChangeLens.Engine.EngineStatus.Extensions;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.Hosting.Helpers;
using ChangeLens.Engine.IntegrationTests.Support;
using ChangeLens.Engine.Logging.Constants;
using ChangeLens.Engine.Logging.Extensions;
using ChangeLens.Engine.Preferences.Extensions;
using ChangeLens.Engine.Protocol.Extensions;
using ChangeLens.Engine.Repositories.Extensions;
using ChangeLens.Infrastructure.LocalState.Constants;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

/// <summary>Verifies claim-checking composition and startup validation invariants.</summary>
public sealed class ClaimCheckingStartupValidationTests
{
    /// <summary>Verifies an enabled checker composes with the model-backed adapter.</summary>
    [Fact]
    public async Task EnabledCheckerStartsWithTheModelAdapter()
    {
        using var temporaryDirectory = new TemporaryDirectory();
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { ContentRootPath = AppContext.BaseDirectory });
        builder.Configuration[LocalStateConstants.DirectoryConfigurationKey] = temporaryDirectory.DirectoryPath;
        builder.Configuration[EngineLoggingConstants.FileDirectoryConfigurationKey] = Path.Combine(temporaryDirectory.DirectoryPath, "logs");
        builder.Configuration[ClaimCheckingConfigurationConstants.EnabledKey] = "true";
        builder.ConfigureContainer(
            new DefaultServiceProviderFactory(new ServiceProviderOptions { ValidateOnBuild = true, ValidateScopes = true }),
            static _ => { });
        builder.AddEngineLogging();
        builder.AddRuntimeServices();
        builder.AddLocalStateServices();
        builder.AddPreferenceServices();
        builder.AddEngineStatusServices();
        builder.AddRepositoryServices();
        builder.AddComparisonServices();
        builder.AddAnalysisServices();
        builder.AddAnalysisRunServices();
        builder.AddProtocolServices();
        builder.AddActionHandlers();

        EngineStartupValidator.Validate(builder.Services);
        using var host = builder.Build();
        await using var scope = host.Services.CreateAsyncScope();

        Assert.True(scope.ServiceProvider.GetRequiredService<ClaimCheckingOptions>().Enabled);
        Assert.IsType<ModelClaimChecker>(scope.ServiceProvider.GetRequiredService<IClaimChecker>());
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IPublicationService>());
    }

    /// <summary>Verifies an enabled checker with a non-positive attempt limit is rejected at startup.</summary>
    [Fact]
    public void EnabledCheckerWithNonPositiveLimitIsRejected()
    {
        var services = new ServiceCollection();
        services.AddSingleton(new ClaimCheckingOptions { Enabled = true, Attempts = 0 });
        services.AddScoped<IClaimChecker, ModelClaimChecker>();

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(services));

        Assert.Contains("must all be positive", exception.Message, StringComparison.Ordinal);
    }
}
