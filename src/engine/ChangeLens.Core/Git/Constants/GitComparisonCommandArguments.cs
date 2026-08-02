namespace ChangeLens.Core.Git.Constants;

/// <summary>
///     Provides the fixed Git argument sequences shared by comparison preparation and snapshot capture.
/// </summary>
internal static class GitComparisonCommandArguments
{
    /// <summary>Prepends the canonical root and fixed safety configuration to a Git subcommand.</summary>
    /// <param name="canonicalPath">The canonical repository root. Cannot be <see langword="null" />.</param>
    /// <param name="subcommandArguments">The fixed Git subcommand arguments. Cannot be <see langword="null" />.</param>
    /// <returns>The complete separate-argument Git invocation.</returns>
    internal static IReadOnlyList<string> Direct(string canonicalPath, IReadOnlyList<string> subcommandArguments) =>
        ["-C", canonicalPath, "-c", "core.fsmonitor=false", "-c", "diff.external=", "-c", "diff.trustExitCode=false", "-c",
            "diff.renames=true", .. subcommandArguments];

    /// <summary>Creates the raw committed-diff arguments between two exact revisions.</summary>
    /// <param name="mergeBaseRevision">The unique merge-base revision. Cannot be <see langword="null" />.</param>
    /// <param name="headRevision">The exact HEAD revision. Cannot be <see langword="null" />.</param>
    /// <returns>The raw-diff subcommand arguments.</returns>
    internal static IReadOnlyList<string> RawDiff(string mergeBaseRevision, string headRevision) =>
        ["diff", "--raw", "-z", "--no-abbrev", "--full-index", "--find-renames=50%", "--no-ext-diff", "--no-textconv",
            mergeBaseRevision, headRevision, "--"];

    /// <summary>Creates the fixed porcelain-v2 status arguments.</summary>
    /// <returns>The status subcommand arguments.</returns>
    internal static IReadOnlyList<string> Status() =>
        ["status", "--porcelain=v2", "-z", "--untracked-files=all", "--ignore-submodules=none", "--find-renames=50%"];
}
