using System.Text.Json;

while (await Console.In.ReadLineAsync() is { } requestLine)
{
    using var request = JsonDocument.Parse(requestLine);
    var requestId = request.RootElement.GetProperty("requestId").GetString()
        ?? throw new InvalidOperationException("The fixture request identifier is required.");

    if (requestId == "desktop-1")
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        continue;
    }

    var response = JsonSerializer.Serialize(new
    {
        protocolVersion = 1,
        type = "result",
        requestId,
        result = new
        {
            name = "ChangeLens.Engine",
            version = "0.1.0",
            protocolVersion = 1,
        },
    });

    await Console.Out.WriteLineAsync(response);
    await Console.Out.FlushAsync();
}
