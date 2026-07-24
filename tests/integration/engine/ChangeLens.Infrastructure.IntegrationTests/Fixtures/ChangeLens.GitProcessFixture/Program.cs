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
            "GIT_EXTERNAL_DIFF",
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

    case "large-stderr":
        await WriteBytesAsync(Console.OpenStandardError(), 65_537);
        return 0;

    case "large-stdout":
        await WriteBytesAsync(Console.OpenStandardOutput(), 128 * 1024);
        return 0;

    case "invalid-utf8":
        await Console.OpenStandardOutput().WriteAsync(new byte[] { 0xc3, 0x28 });
        return 0;

    case "sleep":
        await Task.Delay(TimeSpan.FromMinutes(5));
        return 0;

    case "spawn-child":
        return await SpawnChildAsync(args);

    case "spawn-child-and-large-stdout":
        return await SpawnChildAsync(args, writeLargeStandardOutput: true);

    case "spawn-inheriting-child-and-exit":
        return await SpawnInheritingChildAndExitAsync(args);

    case "success":
        await Console.Out.WriteAsync("fixture standard output");
        await Console.Error.WriteAsync("fixture standard error");
        return 0;

    case "proxy-delete-target-after-check":
        return await ProxyAndDeleteTargetAfterCheckAsync(args);

    case "proxy-change-on-second-status":
        return await ProxyAndChangeOnSecondStatusAsync(args);

    case "nonzero":
        await Console.Out.WriteAsync("fixture nonzero output");
        await Console.Error.WriteAsync("fixture nonzero error");
        return 128;

    default:
        await Console.Error.WriteAsync("Unknown fixture mode.");
        return 2;
}

static async Task<int> ProxyAndDeleteTargetAfterCheckAsync(string[] arguments)
{
    var repository = Environment.GetEnvironmentVariable("CHANGELENS_GIT_FIXTURE_REPOSITORY");
    var target = Environment.GetEnvironmentVariable("CHANGELENS_GIT_FIXTURE_TARGET");
    if (string.IsNullOrEmpty(repository) || string.IsNullOrEmpty(target))
    {
        return 2;
    }

    var exitCode = await RunRealGitAsync(arguments);
    if (exitCode == 0 &&
        arguments.Contains("check-ref-format", StringComparer.Ordinal) &&
        arguments.Contains(target, StringComparer.Ordinal))
    {
        var deletionExitCode = await RunRealGitAsync(
            ["-C", repository, "update-ref", "-d", target],
            suppressOutput: true);
        if (deletionExitCode != 0)
        {
            return deletionExitCode;
        }
    }

    return exitCode;
}

static async Task<int> ProxyAndChangeOnSecondStatusAsync(string[] arguments)
{
    var stateFile = Environment.GetEnvironmentVariable("CHANGELENS_GIT_FIXTURE_STATE_FILE");
    var mutationFile = Environment.GetEnvironmentVariable("CHANGELENS_GIT_FIXTURE_MUTATION_FILE");
    if (string.IsNullOrEmpty(stateFile) || string.IsNullOrEmpty(mutationFile))
    {
        return 2;
    }

    if (arguments.Contains("status", StringComparer.Ordinal) &&
        arguments.Contains("--porcelain=v2", StringComparer.Ordinal))
    {
        var count = 0;
        if (File.Exists(stateFile))
        {
            _ = int.TryParse(
                await File.ReadAllTextAsync(stateFile),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out count);
        }

        count++;
        await File.WriteAllTextAsync(
            stateFile,
            count.ToString(CultureInfo.InvariantCulture));
        if (count == 2)
        {
            await File.WriteAllTextAsync(
                mutationFile,
                "changed during comparison preparation\n");
        }
    }

    return await RunRealGitAsync(arguments);
}

static async Task<int> RunRealGitAsync(
    IReadOnlyList<string> arguments,
    bool suppressOutput = false)
{
    var executable =
        Environment.GetEnvironmentVariable("CHANGELENS_REAL_GIT_EXECUTABLE") ?? "git";
    var startInfo = new ProcessStartInfo(executable)
    {
        CreateNoWindow = true,
        RedirectStandardError = true,
        RedirectStandardOutput = true,
        UseShellExecute = false,
    };

    foreach (var argument in arguments)
    {
        startInfo.ArgumentList.Add(argument);
    }

    using var process = Process.Start(startInfo);
    if (process is null)
    {
        return 127;
    }

    var standardOutput = suppressOutput
        ? Stream.Null
        : Console.OpenStandardOutput();
    var standardError = suppressOutput
        ? Stream.Null
        : Console.OpenStandardError();
    var outputTask = process.StandardOutput.BaseStream.CopyToAsync(standardOutput);
    var errorTask = process.StandardError.BaseStream.CopyToAsync(standardError);
    await process.WaitForExitAsync();
    await Task.WhenAll(outputTask, errorTask);
    return process.ExitCode;
}

static async Task WriteBytesAsync(Stream stream, int count)
{
    var bytes = Encoding.ASCII.GetBytes(new string('x', count));
    await stream.WriteAsync(bytes);
    await stream.FlushAsync();
}

static async Task<int> SpawnChildAsync(
    string[] arguments,
    bool writeLargeStandardOutput = false)
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
    if (writeLargeStandardOutput)
    {
        await WriteBytesAsync(Console.OpenStandardOutput(), 65_537);
    }

    await Task.Delay(TimeSpan.FromMinutes(5));
    return 0;
}

static async Task<int> SpawnInheritingChildAndExitAsync(string[] arguments)
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
        UseShellExecute = false,
    };
    startInfo.ArgumentList.Add(assemblyPath);
    startInfo.Environment["CHANGELENS_GIT_FIXTURE_MODE"] = "sleep";

    using var child = Process.Start(startInfo)
        ?? throw new InvalidOperationException("The child fixture process could not be started.");
    await File.WriteAllTextAsync(
        arguments[0],
        child.Id.ToString(CultureInfo.InvariantCulture));
    return 0;
}
