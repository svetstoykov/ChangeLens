using System.Text.Json;

var mode = args.FirstOrDefault() ?? "success";
var repositoryRequestLogPath = args.Skip(1).FirstOrDefault();
var requestCount = 0;

while (await Console.In.ReadLineAsync() is { } requestLine)
{
    using var request = JsonDocument.Parse(requestLine);
    var requestId = request.RootElement.GetProperty("requestId").GetString()
        ?? throw new InvalidOperationException("The fixture request identifier is required.");
    var action = request.RootElement.GetProperty("action").GetString();

    if (action == "repositories.open")
    {
        var expectedRequest = JsonSerializer.Serialize(new
        {
            protocolVersion = 1,
            requestId,
            action = "repositories.open",
            parameters = new
            {
                path = "/projects/change_lens",
            },
        });

        if (requestLine != expectedRequest)
        {
            throw new InvalidOperationException("The repository request does not match the expected shape.");
        }

        if (repositoryRequestLogPath is not null)
        {
            await File.AppendAllTextAsync(
                repositoryRequestLogPath,
                requestLine + Environment.NewLine);
        }
    }
    else if (action != "engine.checkStatus")
    {
        throw new InvalidOperationException("The fixture received an unsupported action.");
    }

    requestCount++;

    if (mode == "timeout-once" && requestId == "desktop-1")
    {
        await Task.Delay(TimeSpan.FromSeconds(30));
        continue;
    }

    if (mode == "repository-delay-first" && requestId == "desktop-1")
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
        await WriteStatusResultAsync("other-request");
        continue;
    }

    if (mode == "uncorrelated-error-once" && requestCount == 1)
    {
        await WriteJsonAsync(new
        {
            protocolVersion = 1,
            type = "error",
            requestId = (string?)null,
            errors = new[]
            {
                new
                {
                    type = "Validation",
                    code = "protocol.invalidRequest",
                    message = "The request does not match the engine protocol schema.",
                },
            },
        });
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

    if (action == "repositories.open")
    {
        if (mode == "repository-ordered-error-once" && requestCount == 1)
        {
            await WriteOrderedErrorAsync(requestId);
            continue;
        }

        await WriteRepositoryResultAsync(requestId, mode, requestCount);
        continue;
    }

    await WriteStatusResultAsync(requestId);
}

if (mode == "record-eof")
{
    if (repositoryRequestLogPath is null)
    {
        throw new InvalidOperationException("The EOF marker path is required.");
    }

    await File.WriteAllTextAsync(repositoryRequestLogPath, "eof");
}

if (mode == "ignore-eof")
{
    await Task.Delay(Timeout.InfiniteTimeSpan);
}

async Task WriteStatusResultAsync(string requestId)
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

async Task WriteRepositoryResultAsync(string requestId, string fixtureMode, int currentRequestCount)
{
    const string revision = "0123456789abcdef0123456789abcdef01234567";

    object head = fixtureMode switch
    {
        "repository-detached" => new
        {
            kind = "detached",
            revision,
        },
        "repository-wrong-kind-once" when currentRequestCount == 1 => new
        {
            kind = "other",
            revision,
        },
        "repository-detached-name-once" when currentRequestCount == 1 => new
        {
            kind = "detached",
            name = "main",
            revision,
        },
        "repository-branch-missing-name-once" when currentRequestCount == 1 => new
        {
            kind = "branch",
            revision,
        },
        "repository-blank-branch-once" when currentRequestCount == 1 => new
        {
            kind = "branch",
            name = " ",
            revision,
        },
        "repository-uppercase-revision-once" when currentRequestCount == 1 => new
        {
            kind = "branch",
            name = "main",
            revision = revision.ToUpperInvariant(),
        },
        "repository-short-revision-once" when currentRequestCount == 1 => new
        {
            kind = "branch",
            name = "main",
            revision = "0123456789abcdef",
        },
        "repository-nonhex-revision-once" when currentRequestCount == 1 => new
        {
            kind = "branch",
            name = "main",
            revision = "g123456789abcdef0123456789abcdef01234567",
        },
        _ => new
        {
            kind = "branch",
            name = "main",
            revision,
        },
    };

    var name = fixtureMode == "repository-blank-name-once" && currentRequestCount == 1
        ? " "
        : "change_lens";
    var canonicalPath = fixtureMode == "repository-blank-path-once" && currentRequestCount == 1
        ? "\t"
        : "/projects/change_lens";

    await WriteJsonAsync(new
    {
        protocolVersion = 1,
        type = "result",
        requestId,
        result = new
        {
            repository = new
            {
                name,
                canonicalPath,
                head,
            },
        },
    });
}

async Task WriteOrderedErrorAsync(string requestId)
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
}

async Task WriteJsonAsync<T>(T value)
{
    await Console.Out.WriteLineAsync(JsonSerializer.Serialize(value));
    await Console.Out.FlushAsync();
}
