using ChangeLens.Core.Curation.Models;
using ChangeLens.Core.EvidenceBinder.Models;
using ChangeLens.Core.Review.Models;
using ChangeLens.Core.Review.Services;
using ChangeLens.Engine.Hosting.Helpers;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace ChangeLens.Engine.IntegrationTests.Hosting;

/// <summary>Verifies startup rejects binder reserves too small for the reviewer and accepts the shipped defaults.</summary>
public sealed class ReviewerStartupValidationTests
{
    /// <summary>Verifies the default binder, curator, and reviewer options need no change to start.</summary>
    [Fact]
    public void DefaultReservesHoldTheReviewer()
    {
        var exception = Record.Exception(() => ReviewerConfigurationValidator.Validate(new EvidenceBinderOptions(), new ReviewerOptions()));

        Assert.Null(exception);
    }

    /// <summary>Verifies a reviewer reply limit above the binder's output reserve fails startup and names the reserve.</summary>
    [Fact]
    public void ReviewerOutputCharactersAboveTheOutputReserveAreRejected()
    {
        var services = Compose(new EvidenceBinderOptions(), new ReviewerOptions { MaximumOutputCharacters = 176_001 });

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(services));

        Assert.Contains("curator output reserve", exception.Message, StringComparison.Ordinal);
        Assert.Contains("reviewer maximum output characters", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a reviewer token limit whose character estimate exceeds the output reserve fails startup and names the reserve.</summary>
    [Fact]
    public void ReviewerTokenEstimateAboveTheOutputReserveIsRejected()
    {
        var services = Compose(new EvidenceBinderOptions(), new ReviewerOptions { MaximumOutputTokens = 54_200 });

        var exception = Assert.Throws<InvalidOperationException>(() => EngineStartupValidator.Validate(services));

        Assert.Contains("curator output reserve", exception.Message, StringComparison.Ordinal);
        Assert.Contains("reviewer maximum output-token estimate", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies a prompt reserve smaller than the rendered reviewer message fails startup and names the reserve.</summary>
    [Fact]
    public void PromptReserveSmallerThanTheReviewerMessageIsRejected()
    {
        var binderOptions = new EvidenceBinderOptions { PromptReserveCharacters = 100 };

        var exception = Assert.Throws<InvalidOperationException>(
            () => ReviewerConfigurationValidator.Validate(binderOptions, new ReviewerOptions()));

        Assert.Contains("prompt reserve of 100 characters", exception.Message, StringComparison.Ordinal);
        Assert.Contains("rendered reviewer prompt", exception.Message, StringComparison.Ordinal);
    }

    /// <summary>Verifies non-positive reviewer limits are rejected while the reviewer is enabled.</summary>
    /// <param name="characters">The reviewer maximum output characters.</param>
    /// <param name="tokens">The reviewer maximum output tokens.</param>
    [Theory]
    [InlineData(0, 48_000)]
    [InlineData(176_000, 0)]
    public void NonPositiveReviewerLimitsAreRejected(int characters, int tokens)
    {
        var reviewerOptions = new ReviewerOptions { MaximumOutputCharacters = characters, MaximumOutputTokens = tokens };

        Assert.Throws<InvalidOperationException>(() => ReviewerConfigurationValidator.Validate(new EvidenceBinderOptions(), reviewerOptions));
    }

    /// <summary>Verifies a disabled reviewer's limits are not checked against the reserves.</summary>
    [Fact]
    public void DisabledReviewerIsNotValidated()
    {
        var reviewerOptions = new ReviewerOptions { Enabled = false, MaximumOutputCharacters = 10_000_000 };

        var exception = Record.Exception(() => ReviewerConfigurationValidator.Validate(new EvidenceBinderOptions(), reviewerOptions));

        Assert.Null(exception);
    }

    private static ServiceCollection Compose(EvidenceBinderOptions binderOptions, ReviewerOptions reviewerOptions)
    {
        var services = new ServiceCollection();
        services.AddSingleton(binderOptions);
        services.AddSingleton(new CuratorOptions());
        services.AddSingleton(reviewerOptions);
        return services;
    }
}
