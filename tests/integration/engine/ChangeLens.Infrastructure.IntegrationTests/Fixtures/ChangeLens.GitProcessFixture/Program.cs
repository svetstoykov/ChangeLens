using System.Diagnostics;
using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;

const string fixtureModeVariable = "CHANGELENS_GIT_FIXTURE_MODE";
var mode = Environment.GetEnvironmentVariable(fixtureModeVariable);

switch (mode)
{
    case "inspect":
        var environmentNames = new[]
        {
            "GIT_OPTIONAL_LOCKS",
            "GIT_TERMINAL_PROMPT",
            "GCM_INTERACTIVE",
            "GIT_PAGER",
            "PAGER",
            "LC_ALL",
            "LANG",
        };
        var payload = new
        {
            arguments = args,
            environment = environmentNames.ToDictionary(
                name => name,
                Environment.GetEnvironmentVariable),
        };
        await Console.Out.WriteAsync(JsonSerializer.Serialize(payload));
        return 0;

    case "oversized-stdout":
        await WriteBytesAsync(Console.OpenStandardOutput(), 65_537);
        return 0;

    case "oversized-stderr":
        await WriteBytesAsync(Console.OpenStandardError(), 65_537);
        return 0;

    case "invalid-utf8":
        await Console.OpenStandardOutput().WriteAsync(new byte[] { 0xc3, 0x28 });
        return 0;

    case "sleep":
        await Task.Delay(TimeSpan.FromMinutes(5));
        return 0;

    case "spawn-child":
        return await SpawnChildAsync(args);

    case "success":
        await Console.Out.WriteAsync("fixture standard output");
        await Console.Error.WriteAsync("fixture standard error");
        return 0;

    case "nonzero":
        await Console.Out.WriteAsync("fixture nonzero output");
        await Console.Error.WriteAsync("fixture nonzero error");
        return 128;

    default:
        await Console.Error.WriteAsync("Unknown fixture mode.");
        return 2;
}

static async Task WriteBytesAsync(Stream stream, int count)
{
    var bytes = Encoding.ASCII.GetBytes(new string('x', count));
    await stream.WriteAsync(bytes);
    await stream.FlushAsync();
}

static async Task<int> SpawnChildAsync(string[] arguments)
{
    if (arguments.Length != 1)
    {
        return 2;
    }

    var processPath = Environment.ProcessPath
        ?? throw new InvalidOperationException("The fixture process path is unavailable.");
    var assemblyPath = Assembly.GetExecutingAssembly().Location;
    var startInfo = new ProcessStartInfo(processPath)
    {
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add(assemblyPath);
    startInfo.Environment["CHANGELENS_GIT_FIXTURE_MODE"] = "sleep";

    using var child = Process.Start(startInfo)
        ?? throw new InvalidOperationException("The child fixture process could not be started.");
    await File.WriteAllTextAsync(
        arguments[0],
        child.Id.ToString(CultureInfo.InvariantCulture));
    await Task.Delay(TimeSpan.FromMinutes(5));
    return 0;
}
