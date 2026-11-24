using ChangeLens.Core.Analysis.Curation.Constants;
using ChangeLens.Core.Analysis.Curation.Interfaces;
using ChangeLens.Core.Analysis.Curation.Models;
using ChangeLens.Core.Analysis.DraftValidation.Interfaces;
using ChangeLens.Core.Analysis.EvidenceBinder.Constants;
using ChangeLens.Core.ModelCompletion.Interfaces;
using ChangeLens.Engine.Analysis.Extensions;
using ChangeLens.Engine.Hosting.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

/// <summary>Verifies curator composition and reserve startup validation invariants.</summary>
public sealed class CuratorStartupValidationTests
{
    /// <summary>Verifies default composition registers the curation graph without provider credentials.</summary>
    [Fact]
    public void DefaultCompositionBudgetRegistersTheCurationGraphWithoutProviderCredentials()
    {
        var builder = Host.CreateApplicationBuilder();

        builder.AddAnalysisServices();

        var curatorOptions = Assert.Single(builder.Services, descriptor => descriptor.ServiceType == typeof(CuratorOptions));
        Assert.Equal(ServiceLifetime.Singleton, curatorOptions.Lifetime);
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(ICuratorService));
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IDraftValidationService));
        Assert.Contains(builder.Services, descriptor => descriptor.ServiceType == typeof(IModelCompletionClient));
    }

    /// <summary>Verifies an insufficient prompt reserve fails startup validation before any provider call.</summary>
    [Fact]
    public void InsufficientPromptReserveFailsCompositionBeforeAProviderCall()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[EvidenceBinderConfigurationConstants.PromptReserveCharactersKey] = "1";

        builder.AddAnalysisServices();

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(builder.Services));

        Assert.Contains("prompt reserve", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Verifies the binder output reserve must cover the curator output cap and token estimate.</summary>
    [Fact]
    public void OutputReserveMustCoverTheCuratorOutputCapAndTokenEstimate()
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration[EvidenceBinderConfigurationConstants.CuratorOutputCharactersKey] = "10";
        builder.Configuration[CuratorConfigurationConstants.MaximumOutputCharactersKey] = "20";

        builder.AddAnalysisServices();

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(builder.Services));

        Assert.Contains("output", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
