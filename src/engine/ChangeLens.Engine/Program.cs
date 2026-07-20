using ChangeLens.Engine.Extensions;
using ChangeLens.Engine.Protocol.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

builder.AddEngineLogging();

builder.Services.AddSingleton<TextReader>(_ => Console.In);
builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
builder.Services.AddSingleton<EngineProtocolHost>();

using var host = builder.Build();
var protocolHost = host.Services.GetRequiredService<EngineProtocolHost>();
var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("ChangeLens.Engine");

try
{
    await protocolHost.RunAsync(CancellationToken.None);
    return 0;
}
catch (Exception exception)
{
    logger.LogCritical(exception, "The engine terminated unexpectedly.");
    return 1;
}
