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

builder.Logging.ClearProviders();
builder.Services.AddSingleton<TextReader>(_ => Console.In);
builder.Services.AddSingleton<TextWriter>(_ => Console.Out);
builder.Services.AddSingleton<EngineProtocolHost>();

using var host = builder.Build();
var protocolHost = host.Services.GetRequiredService<EngineProtocolHost>();

await protocolHost.RunAsync(CancellationToken.None);
