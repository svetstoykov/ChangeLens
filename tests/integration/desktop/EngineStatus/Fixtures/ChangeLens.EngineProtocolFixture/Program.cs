using System.Text.Json;

var mode = args.FirstOrDefault() ?? "success";
var requestCount = 0;

while (await Console.In.ReadLineAsync() is { } requestLine)
{
    using var request = JsonDocument.Parse(requestLine);
    var requestId = request.RootElement.GetProperty("requestId").GetString()
        ?? throw new InvalidOperationException("The fixture request identifier is required.");
    var action = request.RootElement.GetProperty("action").GetString();
    if (action != "engine.checkStatus")
    {
        throw new InvalidOperationException("The fixture only accepts the engine.checkStatus action.");
    }

    requestCount++;

    if (mode == "timeout-once" && requestId == "desktop-1")
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        continue;
    }

    if (mode == "exit")
    {
        return;
    }

    if (mode == "invalid-json")
    {
        await Console.Out.WriteLineAsync("not-json");
        await Console.Out.FlushAsync();
        continue;
    }

    if (mode == "invalid-utf8")
    {
        await using var output = Console.OpenStandardOutput();
        await output.WriteAsync(new byte[] { 0xff, 0x0a });
        await output.FlushAsync();
        continue;
    }

    if (mode == "oversized")
    {
        await Console.Out.WriteLineAsync(new string('a', 65_536));
        await Console.Out.FlushAsync();
        continue;
    }

    if (mode == "correlation")
    {
        await WriteResultAsync("other-request");
        continue;
    }

    if (mode == "ordered-error-once" && requestCount == 1)
    {
        await WriteJsonAsync(new
        {
            protocolVersion = 1,
            type = "error",
            requestId,
            errors = new[]
            {
                new
                {
                    type = "Validation",
                    code = "fixture.first",
                    message = "The first fixture value is invalid.",
                },
                new
                {
                    type = "Conflict",
                    code = "fixture.second",
                    message = "The second fixture value conflicts with current state.",
                },
            },
        });
        continue;
    }

    await WriteResultAsync(requestId);
}

async Task WriteResultAsync(string requestId)
{
    JsonElement? result = null;
    await WriteJsonAsync(new
    {
        protocolVersion = 1,
        type = "result",
        requestId,
        result,
    });
}

async Task WriteJsonAsync<T>(T value)
{
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value));
    await Console.Out.FlushAsync();
}
