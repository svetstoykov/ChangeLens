using System.Buffers;
using System.Globalization;
using System.Text;
using ChangeLens.Core.Comparisons.Constants;
using ChangeLens.Core.Git.Models;
using ChangeLens.Core.Results.Models;

namespace ChangeLens.Core.Git.Parsers;

/// <summary>
///     Provides strict parsing for reviewed Git comparison command output.
/// </summary>
internal static class GitComparisonOutputParser
{
    /// <summary>
    ///     Rejects unpaired UTF-16 surrogates while enforcing bounded Git output.
    /// </summary>
    private static readonly UTF8Encoding StrictUtf8 = new(false, true);

    /// <summary>
    ///     The single error returned whenever Git output does not match the reviewed grammar.
    /// </summary>
    private static readonly OperationError InspectionFailed =
        OperationError.ExternalDependencyFailure(
            "Git comparison inspection failed.",
            ComparisonErrorCode.InspectionFailed);

    private static readonly SearchValues<char> LowercaseHex =
        SearchValues.Create("0123456789abcdef");

    private static readonly SearchValues<char> AsciiDigits =
        SearchValues.Create("0123456789");

    private static readonly SearchValues<char> ForbiddenReferenceCharacters =
        SearchValues.Create(" ~^:?*[\\");

    /// <summary>
    ///     Parses reference facts emitted by the approved comparison-target discovery format.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing strictly parsed reference records.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    /// <remarks>
    ///     Each line is
    ///     <c>&lt;refname&gt;\0&lt;oid&gt;\0&lt;type&gt;\0&lt;upstream&gt;\0&lt;text&gt;\0\n</c>,
    ///     where upstream and text are optional and reference names are unique across the output.
    /// </remarks>
    internal static Result<IReadOnlyList<GitComparisonTargetRecord>> ParseTargetRecords(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailed;
        }

        var text = output.StandardOutput;

        if (text.Length == 0)
        {
            return Result.Success<IReadOnlyList<GitComparisonTargetRecord>>(
                Array.Empty<GitComparisonTargetRecord>());
        }

        if (!text.EndsWith('\n') || text.Contains('\r'))
        {
            return InspectionFailed;
        }

        var records = new List<GitComparisonTargetRecord>();
        var seenFullNames = new HashSet<string>(StringComparer.Ordinal);
        var lines = new GitFieldReader(text);

        while (lines.TryRead('\n', out var line))
        {
            if (!TryParseTargetRecord(line, out var record, out var fullName) ||
                !seenFullNames.Add(fullName))
            {
                return InspectionFailed;
            }

            records.Add(record);
        }

        return Result.Success<IReadOnlyList<GitComparisonTargetRecord>>(records);
    }

    /// <summary>
    ///     Parses zero or more full merge-base object identifiers.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the merge-base revisions in Git output order.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    /// <remarks>
    ///     Each line is one object identifier; the final line's trailing line feed is optional.
    /// </remarks>
    internal static Result<IReadOnlyList<string>> ParseMergeBases(GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailed;
        }

        var text = output.StandardOutput;

        if (text.Length == 0)
        {
            return Result.Success<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (text.Contains('\r'))
        {
            return InspectionFailed;
        }

        var span = text.EndsWith('\n') ? text.AsSpan()[..^1] : text.AsSpan();
        var revisions = new List<string>();

        while (true)
        {
            var separator = span.IndexOf('\n');
            var line = separator < 0 ? span : span[..separator];

            if (!IsSupportedObjectId(line))
            {
                return InspectionFailed;
            }

            revisions.Add(line.ToString());

            if (separator < 0)
            {
                break;
            }

            span = span[(separator + 1)..];
        }

        return Result.Success<IReadOnlyList<string>>(revisions);
    }

    /// <summary>
    ///     Parses target-only and current-work commit counts from reviewed rev-list output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the left target-only count and right current-work count.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    /// <remarks>
    ///     The single line is <c>&lt;left&gt;\t&lt;right&gt;</c>, and its trailing line feed is
    ///     optional.
    /// </remarks>
    internal static Result<(int TargetOnly, int CurrentWork)> ParseCommitCounts(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output) || output.StandardOutput.Contains('\r'))
        {
            return InspectionFailed;
        }

        var text = output.StandardOutput;
        var line = text.EndsWith('\n') ? text.AsSpan()[..^1] : text.AsSpan();

        if (line.Contains('\n'))
        {
            return InspectionFailed;
        }

        var separator = line.IndexOf('\t');

        if (separator < 0 ||
            !TryParseCanonicalCount(line[..separator], out var targetOnly) ||
            !TryParseCanonicalCount(line[(separator + 1)..], out var currentWork))
        {
            return InspectionFailed;
        }

        return Result.Success((TargetOnly: targetOnly, CurrentWork: currentWork));
    }

    /// <summary>
    ///     Parses committed file and rename-lineage facts from reviewed raw diff output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the committed file records in Git output order.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    /// <remarks>
    ///     Each record is
    ///     <c>:&lt;srcmode&gt; &lt;dstmode&gt; &lt;srcoid&gt; &lt;dstoid&gt; &lt;status&gt;\0&lt;path&gt;\0</c>,
    ///     where a rename status emits the original path followed by the current path. Copy
    ///     statuses are rejected because copy detection is never requested.
    /// </remarks>
    internal static Result<IReadOnlyList<GitComparisonFileRecord>> ParseCommittedFiles(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailed;
        }

        var records = new List<GitComparisonFileRecord>();
        var reader = new GitFieldReader(output.StandardOutput);

        while (!reader.IsAtEnd)
        {
            if (!reader.TryReadNulField(out var header) ||
                !TryParseRawHeader(header, out var isRename))
            {
                return InspectionFailed;
            }

            if (!isRename)
            {
                if (!reader.TryReadNulField(out var path) || path.IsEmpty)
                {
                    return InspectionFailed;
                }

                records.Add(new GitComparisonFileRecord(path.ToString(), null));
                continue;
            }

            if (!reader.TryReadNulField(out var originalPath) ||
                originalPath.IsEmpty ||
                !reader.TryReadNulField(out var currentPath) ||
                currentPath.IsEmpty)
            {
                return InspectionFailed;
            }

            records.Add(
                new GitComparisonFileRecord(currentPath.ToString(), originalPath.ToString()));
        }

        return Result.Success<IReadOnlyList<GitComparisonFileRecord>>(records);
    }

    /// <summary>
    ///     Parses working-tree category and rename-lineage facts from porcelain-v2 output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the working-tree records in Git output order.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    /// <remarks>
    ///     Accepted records, each NUL-terminated:
    ///     <c>1 &lt;XY&gt; &lt;sub&gt; &lt;mH&gt; &lt;mI&gt; &lt;mW&gt; &lt;hH&gt; &lt;hI&gt; &lt;path&gt;</c>;
    ///     <c>2 &lt;XY&gt; &lt;sub&gt; &lt;mH&gt; &lt;mI&gt; &lt;mW&gt; &lt;hH&gt; &lt;hI&gt; &lt;Xscore&gt; &lt;path&gt;</c>
    ///     followed by a separate NUL-terminated original path;
    ///     <c>u &lt;XY&gt; &lt;sub&gt; &lt;m1&gt; &lt;m2&gt; &lt;m3&gt; &lt;mW&gt; &lt;h1&gt; &lt;h2&gt; &lt;h3&gt; &lt;path&gt;</c>;
    ///     <c>? &lt;path&gt;</c>; and <c>! &lt;path&gt;</c>.
    /// </remarks>
    internal static Result<IReadOnlyList<GitWorkingTreeRecord>> ParseWorkingTree(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailed;
        }

        var records = new List<GitWorkingTreeRecord>();
        var reader = new GitFieldReader(output.StandardOutput);

        while (!reader.IsAtEnd)
        {
            if (!reader.TryReadNulField(out var entry) ||
                entry.Length < 3 ||
                !TryParseWorkingTreeEntry(entry, ref reader, out var record))
            {
                return InspectionFailed;
            }

            records.Add(record);
        }

        return Result.Success<IReadOnlyList<GitWorkingTreeRecord>>(records);
    }

    private static bool TryParseTargetRecord(
        ReadOnlySpan<char> line,
        out GitComparisonTargetRecord record,
        out string fullName)
    {
        record = null!;
        fullName = string.Empty;

        if (line.IsEmpty || line[^1] != '\0')
        {
            return false;
        }

        var fields = new GitFieldReader(line[..^1]);

        if (!fields.TryReadNulField(out var name) ||
            !fields.TryReadNulField(out var objectId) ||
            !fields.TryReadNulField(out var objectType) ||
            !fields.TryReadNulField(out var upstream))
        {
            return false;
        }

        var description = fields.Remainder;

        if (description.Contains('\0') ||
            !IsValidFullReference(name) ||
            !IsSupportedObjectId(objectId) ||
            !IsKnownObjectType(objectType) ||
            (!upstream.IsEmpty && !IsValidFullReference(upstream)) ||
            !IsFreeOfControlCharacters(description))
        {
            return false;
        }

        fullName = name.ToString();
        record = new GitComparisonTargetRecord(
            fullName,
            objectId.ToString(),
            objectType.ToString(),
            NullWhenEmpty(upstream),
            NullWhenEmpty(description));
        return true;
    }

    private static bool TryParseRawHeader(ReadOnlySpan<char> header, out bool isRename)
    {
        isRename = false;
        var fields = new GitFieldReader(header);

        if (!fields.TryReadWord(out var prefixedSourceMode) ||
            !fields.TryReadWord(out var targetMode) ||
            !fields.TryReadWord(out var sourceOid) ||
            !fields.TryReadWord(out var targetOid))
        {
            return false;
        }

        var status = fields.Remainder;

        if (prefixedSourceMode.Length != 7 || prefixedSourceMode[0] != ':')
        {
            return false;
        }

        var sourceMode = prefixedSourceMode[1..];

        if (!IsSupportedMode(sourceMode) ||
            !IsSupportedMode(targetMode) ||
            !AreCompatibleObjectIds(sourceOid, targetOid))
        {
            return false;
        }

        if (status is "A" or "D" or "M" or "T")
        {
            return IsConsistentTransition(status[0], sourceMode, targetMode, sourceOid, targetOid);
        }

        isRename = IsPaddedRenameScore(status);

        return isRename &&
               AreBothPresent(sourceMode, targetMode, sourceOid, targetOid) &&
               IsSameFileType(sourceMode, targetMode);
    }

    private static bool TryParseWorkingTreeEntry(
        ReadOnlySpan<char> entry,
        ref GitFieldReader reader,
        out GitWorkingTreeRecord record)
    {
        record = null!;

        return entry[0] switch
        {
            '1' => TryParseOrdinaryEntry(entry, out record),
            '2' => TryParseRenameEntry(entry, ref reader, out record),
            'u' => TryParseUnmergedEntry(entry, out record),
            '?' => TryParsePathOnlyEntry(entry, '?', isUntracked: true, isIgnored: false, out record),
            '!' => TryParsePathOnlyEntry(entry, '!', isUntracked: false, isIgnored: true, out record),
            _ => false,
        };
    }

    private static bool TryParseOrdinaryEntry(
        ReadOnlySpan<char> entry,
        out GitWorkingTreeRecord record)
    {
        record = null!;
        var fields = new GitFieldReader(entry);

        if (!fields.TryReadWord(out var kind) ||
            !fields.TryReadWord(out var status) ||
            !fields.TryReadWord(out var submodule) ||
            !fields.TryReadWord(out var headMode) ||
            !fields.TryReadWord(out var indexMode) ||
            !fields.TryReadWord(out var worktreeMode) ||
            !fields.TryReadWord(out var headOid) ||
            !fields.TryReadWord(out var indexOid))
        {
            return false;
        }

        var path = fields.Remainder;

        if (kind is not "1" ||
            path.IsEmpty ||
            !IsOrdinaryStatus(status) ||
            !IsValidSubmoduleState(submodule) ||
            !IsSupportedMode(headMode) ||
            !IsSupportedMode(indexMode) ||
            !IsSupportedMode(worktreeMode) ||
            !AreCompatibleObjectIds(headOid, indexOid))
        {
            return false;
        }

        if (!IsConsistentSubmoduleState(submodule, IsGitlinkMode(indexMode)) ||
            !IsConsistentIndexState(status[0], headMode, indexMode, headOid, indexOid) ||
            !IsConsistentWorktreeMode(status[1], indexMode, worktreeMode))
        {
            return false;
        }

        record = new GitWorkingTreeRecord(
            path.ToString(),
            null,
            status[0] != '.',
            status[1] != '.',
            false,
            false,
            false);
        return true;
    }

    private static bool TryParseRenameEntry(
        ReadOnlySpan<char> entry,
        ref GitFieldReader reader,
        out GitWorkingTreeRecord record)
    {
        record = null!;
        var fields = new GitFieldReader(entry);

        if (!fields.TryReadWord(out var kind) ||
            !fields.TryReadWord(out var status) ||
            !fields.TryReadWord(out var submodule) ||
            !fields.TryReadWord(out var headMode) ||
            !fields.TryReadWord(out var indexMode) ||
            !fields.TryReadWord(out var worktreeMode) ||
            !fields.TryReadWord(out var headOid) ||
            !fields.TryReadWord(out var indexOid) ||
            !fields.TryReadWord(out var renameScore))
        {
            return false;
        }

        var path = fields.Remainder;

        if (kind is not "2" ||
            path.IsEmpty ||
            !IsRenameStatus(status) ||
            !IsValidSubmoduleState(submodule) ||
            !IsNonzeroSupportedMode(headMode) ||
            !IsNonzeroSupportedMode(indexMode) ||
            !IsSupportedMode(worktreeMode) ||
            !IsSameFileType(headMode, indexMode) ||
            !IsCompactRenameScore(renameScore))
        {
            return false;
        }

        if (!AreCompatibleObjectIds(headOid, indexOid) ||
            IsZeroObjectId(headOid) ||
            IsZeroObjectId(indexOid) ||
            !IsConsistentSubmoduleState(submodule, IsGitlinkMode(indexMode)) ||
            !IsConsistentWorktreeMode(status[1], indexMode, worktreeMode))
        {
            return false;
        }

        if (!reader.TryReadNulField(out var originalPath) || originalPath.IsEmpty)
        {
            return false;
        }

        record = new GitWorkingTreeRecord(
            path.ToString(),
            originalPath.ToString(),
            true,
            status[1] != '.',
            false,
            false,
            false);
        return true;
    }

    private static bool TryParseUnmergedEntry(
        ReadOnlySpan<char> entry,
        out GitWorkingTreeRecord record)
    {
        record = null!;
        var fields = new GitFieldReader(entry);

        if (!fields.TryReadWord(out var kind) ||
            !fields.TryReadWord(out var status) ||
            !fields.TryReadWord(out var submodule) ||
            !fields.TryReadWord(out var stageOneMode) ||
            !fields.TryReadWord(out var stageTwoMode) ||
            !fields.TryReadWord(out var stageThreeMode) ||
            !fields.TryReadWord(out var worktreeMode) ||
            !fields.TryReadWord(out var stageOneOid) ||
            !fields.TryReadWord(out var stageTwoOid) ||
            !fields.TryReadWord(out var stageThreeOid))
        {
            return false;
        }

        var path = fields.Remainder;

        if (kind is not "u" ||
            path.IsEmpty ||
            !IsUnmergedStatus(status) ||
            !IsValidSubmoduleState(submodule) ||
            !IsSupportedMode(stageOneMode) ||
            !IsSupportedMode(stageTwoMode) ||
            !IsSupportedMode(stageThreeMode) ||
            !IsSupportedMode(worktreeMode) ||
            !AreCompatibleObjectIds(stageOneOid, stageTwoOid, stageThreeOid))
        {
            return false;
        }

        var containsGitlink =
            IsGitlinkMode(stageOneMode) ||
            IsGitlinkMode(stageTwoMode) ||
            IsGitlinkMode(stageThreeMode) ||
            IsGitlinkMode(worktreeMode);

        if (!IsConsistentSubmoduleState(submodule, containsGitlink) ||
            !TryGetModeObjectPresence(stageOneMode, stageOneOid, out var stageOne) ||
            !TryGetModeObjectPresence(stageTwoMode, stageTwoOid, out var stageTwo) ||
            !TryGetModeObjectPresence(stageThreeMode, stageThreeOid, out var stageThree) ||
            !IsConsistentUnmergedStages(status, stageOne, stageTwo, stageThree))
        {
            return false;
        }

        record = new GitWorkingTreeRecord(
            path.ToString(),
            null,
            true,
            true,
            false,
            true,
            false);
        return true;
    }

    private static bool TryParsePathOnlyEntry(
        ReadOnlySpan<char> entry,
        char marker,
        bool isUntracked,
        bool isIgnored,
        out GitWorkingTreeRecord record)
    {
        record = null!;

        if (entry[0] != marker || entry[1] != ' ')
        {
            return false;
        }

        record = new GitWorkingTreeRecord(
            entry[2..].ToString(),
            null,
            false,
            false,
            isUntracked,
            false,
            isIgnored);
        return true;
    }

    private static bool IsSuccessfulQuietOutput(GitCommandOutput output)
    {
        if (output.ExitCode != 0 ||
            output.StandardOutput is null ||
            output.StandardError is null ||
            output.StandardError.Length != 0)
        {
            return false;
        }

        try
        {
            return StrictUtf8.GetByteCount(output.StandardOutput) <=
                   ComparisonLimits.MaximumFactOutputBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    /// <summary>
    ///     Validates the mode and object transition implied by a nonrename raw diff status.
    /// </summary>
    /// <param name="status">The raw status character.</param>
    /// <param name="sourceMode">The source mode.</param>
    /// <param name="targetMode">The target mode.</param>
    /// <param name="sourceOid">The source object identifier.</param>
    /// <param name="targetOid">The target object identifier.</param>
    /// <returns><see langword="true" /> when the facts agree with the status.</returns>
    private static bool IsConsistentTransition(
        char status,
        ReadOnlySpan<char> sourceMode,
        ReadOnlySpan<char> targetMode,
        ReadOnlySpan<char> sourceOid,
        ReadOnlySpan<char> targetOid) =>
        status switch
        {
            'A' => IsAbsentMode(sourceMode) &&
                   IsZeroObjectId(sourceOid) &&
                   IsNonzeroSupportedMode(targetMode) &&
                   !IsZeroObjectId(targetOid),
            'D' => IsNonzeroSupportedMode(sourceMode) &&
                   !IsZeroObjectId(sourceOid) &&
                   IsAbsentMode(targetMode) &&
                   IsZeroObjectId(targetOid),
            'M' => AreBothPresent(sourceMode, targetMode, sourceOid, targetOid) &&
                   IsSameFileType(sourceMode, targetMode) &&
                   (!sourceMode.SequenceEqual(targetMode) || !sourceOid.SequenceEqual(targetOid)),
            'T' => AreBothPresent(sourceMode, targetMode, sourceOid, targetOid) &&
                   !IsSameFileType(sourceMode, targetMode),
            _ => false,
        };

    private static bool AreBothPresent(
        ReadOnlySpan<char> sourceMode,
        ReadOnlySpan<char> targetMode,
        ReadOnlySpan<char> sourceOid,
        ReadOnlySpan<char> targetOid) =>
        IsNonzeroSupportedMode(sourceMode) &&
        IsNonzeroSupportedMode(targetMode) &&
        !IsZeroObjectId(sourceOid) &&
        !IsZeroObjectId(targetOid);

    /// <summary>
    ///     Validates the staged half of an ordinary status against the HEAD and index facts.
    /// </summary>
    /// <param name="status">The staged status character.</param>
    /// <param name="headMode">The HEAD mode.</param>
    /// <param name="indexMode">The index mode.</param>
    /// <param name="headOid">The HEAD object identifier.</param>
    /// <param name="indexOid">The index object identifier.</param>
    /// <returns><see langword="true" /> when the facts agree with the status.</returns>
    private static bool IsConsistentIndexState(
        char status,
        ReadOnlySpan<char> headMode,
        ReadOnlySpan<char> indexMode,
        ReadOnlySpan<char> headOid,
        ReadOnlySpan<char> indexOid)
    {
        if (!TryGetModeObjectPresence(headMode, headOid, out var headExists) ||
            !TryGetModeObjectPresence(indexMode, indexOid, out var indexExists))
        {
            return false;
        }

        return status switch
        {
            'A' => !headExists && indexExists,
            'D' => headExists && !indexExists,
            '.' => headExists &&
                   indexExists &&
                   headMode.SequenceEqual(indexMode) &&
                   headOid.SequenceEqual(indexOid),
            'M' => headExists &&
                   indexExists &&
                   IsSameFileType(headMode, indexMode) &&
                   (!headMode.SequenceEqual(indexMode) || !headOid.SequenceEqual(indexOid)),
            'T' => headExists &&
                   indexExists &&
                   !IsSameFileType(headMode, indexMode),
            _ => false,
        };
    }

    /// <summary>
    ///     Validates an unstaged status against the index and worktree mode families.
    /// </summary>
    /// <param name="status">The unstaged status character.</param>
    /// <param name="indexMode">The index mode.</param>
    /// <param name="worktreeMode">The worktree mode.</param>
    /// <returns><see langword="true" /> when the modes agree with the status.</returns>
    private static bool IsConsistentWorktreeMode(
        char status,
        ReadOnlySpan<char> indexMode,
        ReadOnlySpan<char> worktreeMode)
    {
        var indexExists = !IsAbsentMode(indexMode);
        var worktreeExists = !IsAbsentMode(worktreeMode);

        return status switch
        {
            '.' => indexMode.SequenceEqual(worktreeMode),
            'M' => indexExists && worktreeExists && IsSameFileType(indexMode, worktreeMode),
            'T' => indexExists && worktreeExists && !IsSameFileType(indexMode, worktreeMode),
            'D' => indexExists && !worktreeExists,
            _ => false,
        };
    }

    /// <summary>
    ///     Validates that a porcelain submodule marker agrees with the observed gitlink modes.
    /// </summary>
    /// <param name="submoduleState">The four-character porcelain submodule state.</param>
    /// <param name="containsGitlink">Whether any relevant mode is a gitlink.</param>
    /// <returns><see langword="true" /> when the marker and modes agree.</returns>
    private static bool IsConsistentSubmoduleState(
        ReadOnlySpan<char> submoduleState,
        bool containsGitlink) =>
        (submoduleState[0] == 'S') == containsGitlink;

    /// <summary>
    ///     Validates the stage-presence matrix implied by an unmerged conflict code.
    /// </summary>
    /// <param name="status">The two-character unmerged status.</param>
    /// <param name="stageOne">Whether the merge-base stage is present.</param>
    /// <param name="stageTwo">Whether the current-side stage is present.</param>
    /// <param name="stageThree">Whether the target-side stage is present.</param>
    /// <returns><see langword="true" /> when all three stages match the conflict code.</returns>
    private static bool IsConsistentUnmergedStages(
        ReadOnlySpan<char> status,
        bool stageOne,
        bool stageTwo,
        bool stageThree) =>
        status switch
        {
            "DD" => stageOne && !stageTwo && !stageThree,
            "AU" => !stageOne && stageTwo && !stageThree,
            "UD" => stageOne && stageTwo && !stageThree,
            "UA" => !stageOne && !stageTwo && stageThree,
            "DU" => stageOne && !stageTwo && stageThree,
            "AA" => !stageOne && stageTwo && stageThree,
            "UU" => stageOne && stageTwo && stageThree,
            _ => false,
        };

    /// <summary>
    ///     Resolves whether one Git entry exists while requiring its mode and object identifier
    ///     to agree.
    /// </summary>
    /// <param name="mode">The entry mode.</param>
    /// <param name="revision">The entry object identifier.</param>
    /// <param name="isPresent">Whether the entry is present when the shape is valid.</param>
    /// <returns><see langword="true" /> when zero and nonzero facts are consistent.</returns>
    private static bool TryGetModeObjectPresence(
        ReadOnlySpan<char> mode,
        ReadOnlySpan<char> revision,
        out bool isPresent)
    {
        isPresent = !IsAbsentMode(mode);
        return isPresent != IsZeroObjectId(revision);
    }

    private static bool IsOrdinaryStatus(ReadOnlySpan<char> value) =>
        value.Length == 2 &&
        value[0] switch
        {
            '.' => value[1] is 'M' or 'T' or 'D',
            'A' or 'M' or 'T' => value[1] is '.' or 'M' or 'T' or 'D',
            'D' => value[1] == '.',
            _ => false,
        };

    private static bool IsRenameStatus(ReadOnlySpan<char> value) =>
        value.Length == 2 &&
        value[0] == 'R' &&
        value[1] is '.' or 'M' or 'D' or 'T';

    private static bool IsUnmergedStatus(ReadOnlySpan<char> value) =>
        value is "DD" or "AU" or "UD" or "UA" or "DU" or "AA" or "UU";

    private static bool IsValidSubmoduleState(ReadOnlySpan<char> value) =>
        value is "N..." ||
        (value.Length == 4 &&
         value[0] == 'S' &&
         value[1] is '.' or 'C' &&
         value[2] is '.' or 'M' &&
         value[3] is '.' or 'U');

    private static bool IsSupportedMode(ReadOnlySpan<char> value) =>
        value is "000000" or "100644" or "100755" or "120000" or "160000";

    private static bool IsNonzeroSupportedMode(ReadOnlySpan<char> value) =>
        value is "100644" or "100755" or "120000" or "160000";

    private static bool IsAbsentMode(ReadOnlySpan<char> value) => value is "000000";

    private static bool IsGitlinkMode(ReadOnlySpan<char> value) => value is "160000";

    /// <summary>
    ///     Determines whether two modes represent the same Git file type.
    /// </summary>
    /// <param name="left">The first six-digit Git mode.</param>
    /// <param name="right">The second six-digit Git mode.</param>
    /// <returns>
    ///     <see langword="true" /> when both modes represent regular files, symbolic links, or
    ///     gitlinks of the same type.
    /// </returns>
    private static bool IsSameFileType(ReadOnlySpan<char> left, ReadOnlySpan<char> right) =>
        GetFileType(left) == GetFileType(right);

    private static char GetFileType(ReadOnlySpan<char> mode) =>
        mode switch
        {
            "100644" or "100755" => 'f',
            "120000" => 'l',
            "160000" => 'g',
            _ => '\0',
        };

    private static bool AreCompatibleObjectIds(
        ReadOnlySpan<char> left,
        ReadOnlySpan<char> right) =>
        IsSupportedObjectId(left) &&
        IsSupportedObjectId(right) &&
        left.Length == right.Length;

    private static bool AreCompatibleObjectIds(
        ReadOnlySpan<char> first,
        ReadOnlySpan<char> second,
        ReadOnlySpan<char> third) =>
        AreCompatibleObjectIds(first, second) &&
        AreCompatibleObjectIds(second, third);

    private static bool IsSupportedObjectId(ReadOnlySpan<char> value) =>
        value.Length is 40 or 64 &&
        !value.ContainsAnyExcept(LowercaseHex);

    private static bool IsZeroObjectId(ReadOnlySpan<char> value) =>
        !value.IsEmpty && !value.ContainsAnyExcept('0');

    private static bool IsKnownObjectType(ReadOnlySpan<char> value) =>
        value is "blob" or "tree" or "commit" or "tag";

    /// <summary>
    ///     Determines whether a porcelain-v2 rename field carries Git's unpadded similarity score.
    /// </summary>
    /// <param name="value">The rename field, such as <c>R73</c> or <c>R100</c>.</param>
    /// <returns><see langword="true" /> when the field is a supported unpadded rename score.</returns>
    private static bool IsCompactRenameScore(ReadOnlySpan<char> value) =>
        value.Length is 3 or 4 &&
        value[1] != '0' &&
        IsSupportedRenameScore(value);

    /// <summary>
    ///     Determines whether a raw-diff status carries Git's three-digit similarity score.
    /// </summary>
    /// <param name="value">The raw status, such as <c>R073</c> or <c>R100</c>.</param>
    /// <returns><see langword="true" /> when the status is a supported padded rename score.</returns>
    private static bool IsPaddedRenameScore(ReadOnlySpan<char> value) =>
        value.Length == 4 &&
        IsSupportedRenameScore(value);

    /// <summary>
    ///     Determines whether a rename field names a similarity percentage within the requested
    ///     bound.
    /// </summary>
    /// <param name="value">The rename field.</param>
    /// <returns>
    ///     <see langword="true" /> when the field is <c>R</c> followed by a supported percentage.
    /// </returns>
    private static bool IsSupportedRenameScore(ReadOnlySpan<char> value)
    {
        if (value.IsEmpty || value[0] != 'R')
        {
            return false;
        }

        var digits = value[1..];

        return !digits.IsEmpty &&
               !digits.ContainsAnyExcept(AsciiDigits) &&
               int.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out var score) &&
               score is >= ComparisonLimits.RenameSimilarityPercent and <= 100;
    }

    /// <summary>
    ///     Parses a nonnegative decimal count in Git's canonical form.
    /// </summary>
    /// <param name="value">The candidate count.</param>
    /// <param name="result">The parsed count when the form is canonical.</param>
    /// <returns>
    ///     <see langword="true" /> when the value is ASCII digits without a sign, whitespace,
    ///     group separators, redundant leading zeros, or overflow.
    /// </returns>
    private static bool TryParseCanonicalCount(ReadOnlySpan<char> value, out int result)
    {
        if (int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out result) &&
            (value.Length == 1 || value[0] != '0'))
        {
            return true;
        }

        result = 0;
        return false;
    }

    /// <summary>
    ///     Determines whether a value is a fully qualified Git reference name.
    /// </summary>
    /// <param name="value">The candidate reference name.</param>
    /// <returns><see langword="true" /> when the name matches the accepted shape.</returns>
    /// <remarks>
    ///     Accepted shape is <c>refs/&lt;component&gt;(/&lt;component&gt;)*</c>. Every component
    ///     is nonempty, does not begin with <c>.</c>, and does not end with <c>.lock</c>. The
    ///     whole name rejects control characters, the characters <c> ~^:?*[\</c>, the sequences
    ///     <c>..</c>, <c>//</c> and <c>@{</c>, and a trailing <c>/</c> or <c>.</c>.
    /// </remarks>
    private static bool IsValidFullReference(ReadOnlySpan<char> value)
    {
        const string prefix = "refs/";

        if (!value.StartsWith(prefix, StringComparison.Ordinal) ||
            value.Length == prefix.Length ||
            value[^1] is '/' or '.' ||
            value.ContainsAny(ForbiddenReferenceCharacters) ||
            !IsFreeOfControlCharacters(value) ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal))
        {
            return false;
        }

        var remaining = value;

        while (!remaining.IsEmpty)
        {
            var separator = remaining.IndexOf('/');
            var component = separator < 0 ? remaining : remaining[..separator];

            if (component.IsEmpty ||
                component[0] == '.' ||
                component.EndsWith(".lock", StringComparison.Ordinal))
            {
                return false;
            }

            remaining = separator < 0 ? default : remaining[(separator + 1)..];
        }

        return true;
    }

    /// <summary>
    ///     Determines whether a value is free of Unicode control characters.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <returns><see langword="true" /> when no control character is present.</returns>
    /// <remarks>
    ///     Unpaired surrogates are already rejected for the whole payload by
    ///     <see cref="IsSuccessfulQuietOutput" />, so no per-field scalar validation is performed.
    /// </remarks>
    private static bool IsFreeOfControlCharacters(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (char.IsControl(character))
            {
                return false;
            }
        }

        return true;
    }

    private static string? NullWhenEmpty(ReadOnlySpan<char> value) =>
        value.IsEmpty ? null : value.ToString();

    /// <summary>
    ///     Walks delimited Git output without allocating intermediate strings.
    /// </summary>
    private ref struct GitFieldReader
    {
        private readonly ReadOnlySpan<char> _text;
        private int _position;

        internal GitFieldReader(ReadOnlySpan<char> text)
        {
            this._text = text;
            this._position = 0;
        }

        /// <summary>
        ///     Gets a value indicating whether every character has been consumed.
        /// </summary>
        internal readonly bool IsAtEnd => this._position >= this._text.Length;

        /// <summary>
        ///     Gets the unconsumed remainder, which is how trailing fields that may contain the
        ///     delimiter are read.
        /// </summary>
        internal readonly ReadOnlySpan<char> Remainder => this._text[this._position..];

        /// <summary>
        ///     Reads the text up to the next delimiter and consumes the delimiter.
        /// </summary>
        /// <param name="delimiter">The delimiter that terminates the field.</param>
        /// <param name="field">The field text, excluding the delimiter.</param>
        /// <returns><see langword="false" /> when no delimiter remains.</returns>
        internal bool TryRead(char delimiter, out ReadOnlySpan<char> field)
        {
            var offset = this.Remainder.IndexOf(delimiter);
            if (offset < 0)
            {
                field = default;
                return false;
            }

            field = this._text.Slice(this._position, offset);
            this._position += offset + 1;
            return true;
        }

        /// <summary>
        ///     Reads a NUL-terminated field.
        /// </summary>
        /// <param name="field">The field text, excluding the terminator.</param>
        /// <returns><see langword="false" /> when no terminator remains.</returns>
        internal bool TryReadNulField(out ReadOnlySpan<char> field) => this.TryRead('\0', out field);

        /// <summary>
        ///     Reads a nonempty space-terminated field.
        /// </summary>
        /// <param name="field">The field text, excluding the separating space.</param>
        /// <returns><see langword="false" /> when the field is missing or empty.</returns>
        internal bool TryReadWord(out ReadOnlySpan<char> field) =>
            this.TryRead(' ', out field) && !field.IsEmpty;
    }
}
