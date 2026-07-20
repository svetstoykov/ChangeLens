using ChangeLens.Engine.Protocol.Services;

var protocolHost = new EngineProtocolHost(Console.In, Console.Out);
await protocolHost.RunAsync(CancellationToken.None);
