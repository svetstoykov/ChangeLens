using System.Text.RegularExpressions;

namespace ChangeLens.Engine.IntegrationTests.Protocol.Support;

/// <summary>
///     Provides level-based projections of a log captured from a real engine process.
/// </summary>
/// <remarks>
///     <para>
///         Local filesystem paths, Git command arguments, and the exception text that quotes a path are
///         sensitive and must never reach a level an operator sees, while <c>Debug</c> deliberately records
///         them so a failing invocation can be reproduced. Assert a path against
///         <see cref="InformationAndAbove" /> so both suites enforce that one boundary; assert protocol
///         payloads, freshness tokens, and caller-supplied query text against the whole log.
///     </para>
/// </remarks>
internal static partial class EngineLogEntries
{
    /// <summary>Selects the log entries written at <c>Information</c> or above.</summary>
    /// <param name="log">The captured log text, from either the file sink or standard error.</param>
    /// <returns>The log text with every <c>Debug</c> and <c>Verbose</c> entry removed.</returns>
    internal static string InformationAndAbove(string log)
    {
        var retained = new List<string>();
        var inSuppressedEntry = false;

        foreach (var line in log.Split('\n'))
        {
            if (EntryLevel().Match(AnsiEscapes().Replace(line, string.Empty)) is { Success: true } entryStart)
            {
                inSuppressedEntry = entryStart.Groups["level"].Value is "DBG" or "VRB";
            }

            if (!inSuppressedEntry)
            {
                retained.Add(line);
            }
        }

        return string.Join('\n', retained);
    }

    [GeneratedRegex(@"^\[[^\]]* (?<level>[A-Z]{3})\] \[")]
    private static partial Regex EntryLevel();

    [GeneratedRegex(@"\x1B\[[0-9;]*m")]
    private static partial Regex AnsiEscapes();
}
