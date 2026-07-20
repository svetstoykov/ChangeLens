using ChangeLens.Engine.Hosting.Extensions;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(
    new HostApplicationBuilderSettings
    {
        Args = args,
        ContentRootPath = AppContext.BaseDirectory,
    });

builder.AddEngine();

using var host = builder.Build();
return await host.RunEngineAsync();
