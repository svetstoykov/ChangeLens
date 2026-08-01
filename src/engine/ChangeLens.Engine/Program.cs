using ChangeLens.Core.LocalState.Interfaces;
using ChangeLens.Core.Results.Models;
using ChangeLens.Engine.Hosting.Constants;
using ChangeLens.Engine.Hosting.Extensions;
using ChangeLens.Engine.Hosting.Services;
using ChangeLens.Engine.Logging.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

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
builder.AddAnalysisRunServices();
builder.AddProtocolServices();
builder.AddActionHandlers();

EngineStartupValidator.Validate(builder.Services);

using var host = builder.Build();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger(EngineProcessConstants.ApplicationName);

try
{
    // Local state must be ready before the protocol host reads its first request, so it is initialized once, up
    // front, instead of gating every action behind a per-request readiness check.
    Result initializationResult;
    await using (var bootScope = host.Services.CreateAsyncScope())
    {
        var localStateInitializer = bootScope.ServiceProvider.GetRequiredService<ILocalStateInitializer>();
        initializationResult = await localStateInitializer.InitializeAsync(CancellationToken.None);
    }

    if (initializationResult.IsFailure)
    {
        logger.LogCritical(
            "The engine cannot start because local state failed to initialize. Errors: {ErrorCodes}",
            initializationResult.Errors.Select(error => error.Code));
        return EngineProcessConstants.UnexpectedFailureExitCode;
    }

    await host.RunAsync();
    return Environment.ExitCode;
}
catch (Exception exception)
{
    logger.LogCritical(exception, EngineProcessConstants.UnexpectedTerminationLogMessage);
    return EngineProcessConstants.UnexpectedFailureExitCode;
}
