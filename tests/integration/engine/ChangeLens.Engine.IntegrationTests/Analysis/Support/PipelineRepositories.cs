using ChangeLens.Engine.IntegrationTests.Protocol.Support;

namespace ChangeLens.Engine.IntegrationTests.Analysis.Support;

/// <summary>
///     Builds the small Git repositories the scripted review runs analyze.
/// </summary>
internal static class PipelineRepositories
{
    /// <summary>
    ///     Marker text the committed change introduces, which scripted replies cite.
    /// </summary>
    internal const string CommittedMarker = "committed-marker";

    /// <summary>
    ///     Creates a repository whose head commit changes one source file so it returns the committed marker.
    /// </summary>
    /// <returns>The temporary repository the caller must dispose.</returns>
    internal static ProtocolTemporaryGitRepository CreateCommittedChange()
    {
        var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("src/app.txt", "public static string Value()\n{\n    return \"original\";\n}\n");
        repository.CommitFileAtHead("src/app.txt", $"public static string Value()\n{{\n    return \"{CommittedMarker}\";\n}}\n");
        return repository;
    }

    /// <summary>
    ///     Creates a repository whose head commit changes the marker file and one further file whose single hunk quotes
    ///     more than the poll response budget allows.
    /// </summary>
    /// <returns>The temporary repository the caller must dispose.</returns>
    internal static ProtocolTemporaryGitRepository CreateCommittedChangeWithOversizedQuote()
    {
        const int lines = 59;
        const int lineCharacters = 40_000;
        var original = string.Join("\n", Enumerable.Range(0, lines).Select(index => $"short line {index}")) + "\n";
        var filler = string.Concat(Enumerable.Repeat("lorem ipsum ", lineCharacters / 12));
        var oversized = string.Join("\n", Enumerable.Range(0, lines).Select(index => $"line {index} {filler}")) + "\n";
        var repository = new ProtocolTemporaryGitRepository();
        repository.CommitFile("src/app.txt", "public static string Value()\n{\n    return \"original\";\n}\n");
        repository.CommitFile("src/big.txt", original);
        repository.CommitFileAtHead("src/app.txt", $"public static string Value()\n{{\n    return \"{CommittedMarker}\";\n}}\n");
        repository.CommitFileAtHead("src/big.txt", oversized);
        return repository;
    }
}
