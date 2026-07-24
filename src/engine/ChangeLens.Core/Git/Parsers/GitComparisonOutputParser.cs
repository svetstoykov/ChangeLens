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
    ///     Parses reference facts emitted by the approved comparison-target discovery format.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing strictly parsed reference records.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<IReadOnlyList<GitComparisonTargetRecord>> ParseTargetRecords(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailure<IReadOnlyList<GitComparisonTargetRecord>>();
        }

        if (output.StandardOutput.Length == 0)
        {
            return Result.Success<IReadOnlyList<GitComparisonTargetRecord>>(
                Array.Empty<GitComparisonTargetRecord>());
        }

        if (!output.StandardOutput.EndsWith('\n') ||
            output.StandardOutput.Contains('\r'))
        {
            return InspectionFailure<IReadOnlyList<GitComparisonTargetRecord>>();
        }

        var lines = output.StandardOutput.Split('\n');
        var records = new List<GitComparisonTargetRecord>(lines.Length - 1);

        for (var index = 0; index < lines.Length - 1; index++)
        {
            var line = lines[index];

            if (line.Length == 0 || !line.EndsWith('\0'))
            {
                return InspectionFailure<IReadOnlyList<GitComparisonTargetRecord>>();
            }

            var fields = line[..^1].Split('\0');
            if (fields.Length != 5 ||
                !IsValidFullReference(fields[0]) ||
                !IsSupportedObjectId(fields[1]) ||
                !IsKnownObjectType(fields[2]) ||
                fields[3].Length > 0 && !IsValidFullReference(fields[3]) ||
                !IsValidOptionalText(fields[4]))
            {
                return InspectionFailure<IReadOnlyList<GitComparisonTargetRecord>>();
            }

            records.Add(
                new GitComparisonTargetRecord(
                    fields[0],
                    fields[1],
                    fields[2],
                    NullWhenEmpty(fields[3]),
                    NullWhenEmpty(fields[4])));
        }

        return lines[^1].Length == 0
            ? Result.Success<IReadOnlyList<GitComparisonTargetRecord>>(records)
            : InspectionFailure<IReadOnlyList<GitComparisonTargetRecord>>();
    }

    /// <summary>
    ///     Parses zero or more full merge-base object identifiers.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the merge-base revisions in Git output order.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<IReadOnlyList<string>> ParseMergeBases(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailure<IReadOnlyList<string>>();
        }

        if (output.StandardOutput.Length == 0)
        {
            return Result.Success<IReadOnlyList<string>>(Array.Empty<string>());
        }

        if (output.StandardOutput.Contains('\r'))
        {
            return InspectionFailure<IReadOnlyList<string>>();
        }

        var text = RemoveOneTerminalLineFeed(output.StandardOutput);
        var revisions = text.Split('\n');

        return revisions.Length > 0 && revisions.All(IsSupportedObjectId)
            ? Result.Success<IReadOnlyList<string>>(revisions)
            : InspectionFailure<IReadOnlyList<string>>();
    }

    /// <summary>
    ///     Parses target-only and current-work commit counts from reviewed rev-list output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the left target-only count and right current-work count.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<(int TargetOnly, int CurrentWork)> ParseCommitCounts(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output) ||
            output.StandardOutput.Length == 0 ||
            output.StandardOutput.Contains('\r'))
        {
            return InspectionFailure<(int TargetOnly, int CurrentWork)>();
        }

        var text = RemoveOneTerminalLineFeed(output.StandardOutput);
        if (text.Contains('\n'))
        {
            return InspectionFailure<(int TargetOnly, int CurrentWork)>();
        }

        var fields = text.Split('\t');
        return fields.Length == 2 &&
               TryParseCanonicalCount(fields[0], out var targetOnly) &&
               TryParseCanonicalCount(fields[1], out var currentWork)
            ? Result.Success((TargetOnly: targetOnly, CurrentWork: currentWork))
            : InspectionFailure<(int TargetOnly, int CurrentWork)>();
    }

    /// <summary>
    ///     Parses committed file and rename-lineage facts from reviewed raw diff output.
    /// </summary>
    /// <param name="output">The captured Git output. Cannot be <see langword="null" />.</param>
    /// <returns>A result containing the committed file records in Git output order.</returns>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="output" /> is <see langword="null" />.
    /// </exception>
    internal static Result<IReadOnlyList<GitComparisonFileRecord>> ParseCommittedFiles(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailure<IReadOnlyList<GitComparisonFileRecord>>();
        }

        var records = new List<GitComparisonFileRecord>();
        var position = 0;

        while (position < output.StandardOutput.Length)
        {
            if (!TryReadNulField(output.StandardOutput, ref position, out var header) ||
                !TryParseRawHeader(
                    header,
                    out var status,
                    out var isRename))
            {
                return InspectionFailure<IReadOnlyList<GitComparisonFileRecord>>();
            }

            if (!TryReadNulField(output.StandardOutput, ref position, out var firstPath) ||
                firstPath.Length == 0)
            {
                return InspectionFailure<IReadOnlyList<GitComparisonFileRecord>>();
            }

            if (!isRename)
            {
                records.Add(new GitComparisonFileRecord(firstPath, null));
                continue;
            }

            if (!TryReadNulField(output.StandardOutput, ref position, out var currentPath) ||
                currentPath.Length == 0 ||
                status[0] != 'R')
            {
                return InspectionFailure<IReadOnlyList<GitComparisonFileRecord>>();
            }

            records.Add(new GitComparisonFileRecord(currentPath, firstPath));
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
    internal static Result<IReadOnlyList<GitWorkingTreeRecord>> ParseWorkingTree(
        GitCommandOutput output)
    {
        ArgumentNullException.ThrowIfNull(output);

        if (!IsSuccessfulQuietOutput(output))
        {
            return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
        }

        var records = new List<GitWorkingTreeRecord>();
        var position = 0;

        while (position < output.StandardOutput.Length)
        {
            if (!TryReadNulField(output.StandardOutput, ref position, out var record) ||
                record.Length < 3)
            {
                return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
            }

            switch (record[0])
            {
                case '1':
                    if (!TryParseOrdinaryRecord(record, out var ordinary))
                    {
                        return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
                    }

                    records.Add(ordinary);
                    break;
                case '2':
                    if (!TryParseRenameRecord(
                            output.StandardOutput,
                            ref position,
                            record,
                            out var renamed))
                    {
                        return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
                    }

                    records.Add(renamed);
                    break;
                case 'u':
                    if (!TryParseUnmergedRecord(record, out var unmerged))
                    {
                        return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
                    }

                    records.Add(unmerged);
                    break;
                case '?':
                    if (!record.StartsWith("? ", StringComparison.Ordinal) ||
                        record.Length == 2)
                    {
                        return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
                    }

                    records.Add(
                        new GitWorkingTreeRecord(
                            record[2..],
                            null,
                            false,
                            false,
                            true,
                            false,
                            false));
                    break;
                case '!':
                    if (!record.StartsWith("! ", StringComparison.Ordinal) ||
                        record.Length == 2)
                    {
                        return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
                    }

                    records.Add(
                        new GitWorkingTreeRecord(
                            record[2..],
                            null,
                            false,
                            false,
                            false,
                            false,
                            true));
                    break;
                default:
                    return InspectionFailure<IReadOnlyList<GitWorkingTreeRecord>>();
            }
        }

        return Result.Success<IReadOnlyList<GitWorkingTreeRecord>>(records);
    }

    private static bool TryParseOrdinaryRecord(
        string record,
        out GitWorkingTreeRecord result)
    {
        result = null!;

        if (!TrySplitPrefix(record, 8, out var fields, out var path) ||
            fields[0] != "1" ||
            !IsOrdinaryStatus(fields[1]) ||
            !IsValidSubmoduleState(fields[2]) ||
            !IsSupportedMode(fields[3]) ||
            !IsSupportedMode(fields[4]) ||
            !IsSupportedMode(fields[5]) ||
            !IsConsistentSubmoduleState(fields[2], fields[4]) ||
            !AreCompatibleObjectIds(fields[6], fields[7]) ||
            !IsConsistentOrdinaryState(
                fields[1],
                fields[3],
                fields[4],
                fields[5],
                fields[6],
                fields[7]) ||
            path.Length == 0)
        {
            return false;
        }

        result = new GitWorkingTreeRecord(
            path,
            null,
            fields[1][0] != '.',
            fields[1][1] != '.',
            false,
            false,
            false);
        return true;
    }

    private static bool TryParseRenameRecord(
        string output,
        ref int position,
        string record,
        out GitWorkingTreeRecord result)
    {
        result = null!;

        if (!TrySplitPrefix(record, 9, out var fields, out var path) ||
            fields[0] != "2" ||
            !IsRenameStatus(fields[1]) ||
            !IsValidSubmoduleState(fields[2]) ||
            !IsNonzeroSupportedMode(fields[3]) ||
            !IsNonzeroSupportedMode(fields[4]) ||
            !IsSameFileType(fields[3], fields[4]) ||
            !IsConsistentSubmoduleState(fields[2], fields[4]) ||
            !IsConsistentWorktreeMode(fields[1][1], fields[4], fields[5]) ||
            !AreCompatibleObjectIds(fields[6], fields[7]) ||
            IsZeroObjectId(fields[6]) ||
            IsZeroObjectId(fields[7]) ||
            !IsRenameScore(fields[8]) ||
            path.Length == 0 ||
            !TryReadNulField(output, ref position, out var originalPath) ||
            originalPath.Length == 0)
        {
            return false;
        }

        result = new GitWorkingTreeRecord(
            path,
            originalPath,
            true,
            fields[1][1] != '.',
            false,
            false,
            false);
        return true;
    }

    private static bool TryParseUnmergedRecord(
        string record,
        out GitWorkingTreeRecord result)
    {
        result = null!;

        if (!TrySplitPrefix(record, 10, out var fields, out var path) ||
            fields[0] != "u" ||
            !IsUnmergedStatus(fields[1]) ||
            !IsValidSubmoduleState(fields[2]) ||
            !fields.Skip(3).Take(4).All(IsSupportedMode) ||
            !AreCompatibleObjectIds(fields[7], fields[8], fields[9]) ||
            !IsConsistentUnmergedStages(
                fields[1],
                fields[3],
                fields[4],
                fields[5],
                fields[7],
                fields[8],
                fields[9]) ||
            !IsConsistentSubmoduleState(
                fields[2],
                fields[3],
                fields[4],
                fields[5],
                fields[6]) ||
            path.Length == 0)
        {
            return false;
        }

        result = new GitWorkingTreeRecord(
            path,
            null,
            true,
            true,
            false,
            true,
            false);
        return true;
    }

    private static bool TryParseRawHeader(
        string header,
        out string status,
        out bool isRename)
    {
        status = string.Empty;
        isRename = false;
        var fields = header.Split(' ');

        if (fields.Length != 5 ||
            fields[0].Length != 7 ||
            fields[0][0] != ':' ||
            !IsSupportedMode(fields[0][1..]) ||
            !IsSupportedMode(fields[1]) ||
            !AreCompatibleObjectIds(fields[2], fields[3]))
        {
            return false;
        }

        status = fields[4];
        var oldMode = fields[0][1..];
        var newMode = fields[1];
        var oldRevision = fields[2];
        var newRevision = fields[3];

        switch (status)
        {
            case "A":
                return oldMode == "000000" &&
                       IsZeroObjectId(oldRevision) &&
                       IsNonzeroSupportedMode(newMode) &&
                       !IsZeroObjectId(newRevision);
            case "D":
                return IsNonzeroSupportedMode(oldMode) &&
                       !IsZeroObjectId(oldRevision) &&
                       newMode == "000000" &&
                       IsZeroObjectId(newRevision);
            case "M":
                return IsNonzeroSupportedMode(oldMode) &&
                       IsNonzeroSupportedMode(newMode) &&
                       IsSameFileType(oldMode, newMode) &&
                       !IsZeroObjectId(oldRevision) &&
                       !IsZeroObjectId(newRevision) &&
                       (oldMode != newMode || oldRevision != newRevision);
            case "T":
                return IsNonzeroSupportedMode(oldMode) &&
                       IsNonzeroSupportedMode(newMode) &&
                       !IsSameFileType(oldMode, newMode) &&
                       !IsZeroObjectId(oldRevision) &&
                       !IsZeroObjectId(newRevision);
            default:
                isRename = IsRenameScore(status);
                return isRename &&
                       IsNonzeroSupportedMode(oldMode) &&
                       IsNonzeroSupportedMode(newMode) &&
                       IsSameFileType(oldMode, newMode) &&
                       !IsZeroObjectId(oldRevision) &&
                       !IsZeroObjectId(newRevision);
        }
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
                   ComparisonLimits.MaximumFactOutputBytes &&
                   StrictUtf8.GetByteCount(output.StandardError) <=
                   ComparisonLimits.MaximumDiagnosticBytes;
        }
        catch (EncoderFallbackException)
        {
            return false;
        }
    }

    private static bool TryReadNulField(
        string text,
        ref int position,
        out string field)
    {
        var terminator = text.IndexOf('\0', position);
        if (terminator < 0)
        {
            field = string.Empty;
            return false;
        }

        field = text[position..terminator];
        position = terminator + 1;
        return true;
    }

    private static bool TrySplitPrefix(
        string value,
        int fieldCount,
        out string[] fields,
        out string remainder)
    {
        fields = new string[fieldCount];
        var position = 0;

        for (var index = 0; index < fieldCount; index++)
        {
            var separator = value.IndexOf(' ', position);
            if (separator < 0)
            {
                remainder = string.Empty;
                return false;
            }

            fields[index] = value[position..separator];
            if (fields[index].Length == 0)
            {
                remainder = string.Empty;
                return false;
            }

            position = separator + 1;
        }

        remainder = value[position..];
        return true;
    }

    private static bool IsOrdinaryStatus(string value) =>
        value.Length == 2 &&
        value[0] switch
        {
            '.' => value[1] is 'M' or 'T' or 'D',
            'A' or 'M' or 'T' => value[1] is '.' or 'M' or 'T' or 'D',
            'D' => value[1] == '.',
            _ => false,
        };

    private static bool IsRenameStatus(string value) =>
        value.Length == 2 &&
        value[0] == 'R' &&
        value[1] is '.' or 'M' or 'D' or 'T';

    private static bool IsUnmergedStatus(string value) =>
        value is "DD" or "AU" or "UD" or "UA" or "DU" or "AA" or "UU";

    private static bool IsValidSubmoduleState(string value) =>
        value == "N..." ||
        value.Length == 4 &&
        value[0] == 'S' &&
        value[1] is '.' or 'C' &&
        value[2] is '.' or 'M' &&
        value[3] is '.' or 'U';

    /// <summary>
    ///     Validates staged object and mode facts plus the unstaged mode transition.
    /// </summary>
    /// <param name="status">The two-character ordinary porcelain status.</param>
    /// <param name="headMode">The HEAD mode.</param>
    /// <param name="indexMode">The index mode.</param>
    /// <param name="worktreeMode">The worktree mode.</param>
    /// <param name="headRevision">The HEAD object identifier.</param>
    /// <param name="indexRevision">The index object identifier.</param>
    /// <returns><see langword="true" /> when the facts agree with the status.</returns>
    private static bool IsConsistentOrdinaryState(
        string status,
        string headMode,
        string indexMode,
        string worktreeMode,
        string headRevision,
        string indexRevision)
    {
        if (!TryGetModeObjectPresence(headMode, headRevision, out var headExists) ||
            !TryGetModeObjectPresence(indexMode, indexRevision, out var indexExists))
        {
            return false;
        }

        var indexConsistent = status[0] switch
        {
            'A' => !headExists && indexExists,
            'D' => headExists && !indexExists,
            '.' => headExists &&
                   indexExists &&
                   headMode == indexMode &&
                   headRevision == indexRevision,
            'M' => headExists &&
                   indexExists &&
                   IsSameFileType(headMode, indexMode) &&
                   (headMode != indexMode || headRevision != indexRevision),
            'T' => headExists &&
                   indexExists &&
                   !IsSameFileType(headMode, indexMode),
            _ => false,
        };

        var worktreeConsistent =
            IsConsistentWorktreeMode(status[1], indexMode, worktreeMode);

        return indexConsistent && worktreeConsistent;
    }

    /// <summary>
    ///     Validates an unstaged status against index and worktree mode families.
    /// </summary>
    /// <param name="status">The unstaged status character.</param>
    /// <param name="indexMode">The index mode.</param>
    /// <param name="worktreeMode">The worktree mode.</param>
    /// <returns><see langword="true" /> when the modes agree with the status.</returns>
    private static bool IsConsistentWorktreeMode(
        char status,
        string indexMode,
        string worktreeMode)
    {
        var indexExists = indexMode != "000000";
        var worktreeExists = worktreeMode != "000000";

        return status switch
        {
            '.' => indexMode == worktreeMode,
            'M' => indexExists &&
                   worktreeExists &&
                   IsSameFileType(indexMode, worktreeMode),
            'T' => indexExists &&
                   worktreeExists &&
                   !IsSameFileType(indexMode, worktreeMode),
            'D' => indexExists && !worktreeExists,
            _ => false,
        };
    }

    /// <summary>
    ///     Validates that a porcelain submodule marker matches the relevant Git modes.
    /// </summary>
    /// <param name="submoduleState">The four-character porcelain submodule state.</param>
    /// <param name="modes">The modes whose current stages determine whether the entry is a gitlink.</param>
    /// <returns><see langword="true" /> when the marker and modes agree.</returns>
    private static bool IsConsistentSubmoduleState(
        string submoduleState,
        params string[] modes)
    {
        var containsGitlink = modes.Any(mode => mode == "160000");
        return submoduleState[0] == 'S'
            ? containsGitlink
            : !containsGitlink;
    }

    /// <summary>
    ///     Validates the stage-presence matrix and zero identifiers for an unmerged status.
    /// </summary>
    /// <param name="status">The two-character unmerged status.</param>
    /// <param name="stageOneMode">The merge-base stage mode.</param>
    /// <param name="stageTwoMode">The current-side stage mode.</param>
    /// <param name="stageThreeMode">The target-side stage mode.</param>
    /// <param name="stageOneRevision">The merge-base stage object identifier.</param>
    /// <param name="stageTwoRevision">The current-side stage object identifier.</param>
    /// <param name="stageThreeRevision">The target-side stage object identifier.</param>
    /// <returns><see langword="true" /> when all three stages match the conflict code.</returns>
    private static bool IsConsistentUnmergedStages(
        string status,
        string stageOneMode,
        string stageTwoMode,
        string stageThreeMode,
        string stageOneRevision,
        string stageTwoRevision,
        string stageThreeRevision)
    {
        if (!TryGetModeObjectPresence(stageOneMode, stageOneRevision, out var stageOne) ||
            !TryGetModeObjectPresence(stageTwoMode, stageTwoRevision, out var stageTwo) ||
            !TryGetModeObjectPresence(stageThreeMode, stageThreeRevision, out var stageThree))
        {
            return false;
        }

        return status switch
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
    }

    /// <summary>
    ///     Resolves whether one Git entry exists while requiring its mode and object identifier to agree.
    /// </summary>
    /// <param name="mode">The entry mode.</param>
    /// <param name="revision">The entry object identifier.</param>
    /// <param name="isPresent">Whether the entry is present when the shape is valid.</param>
    /// <returns><see langword="true" /> when zero and nonzero facts are consistent.</returns>
    private static bool TryGetModeObjectPresence(
        string mode,
        string revision,
        out bool isPresent)
    {
        isPresent = mode != "000000";
        return isPresent != IsZeroObjectId(revision);
    }

    private static bool IsSupportedMode(string value) =>
        value is "000000" or "100644" or "100755" or "120000" or "160000";

    private static bool IsNonzeroSupportedMode(string value) =>
        value is "100644" or "100755" or "120000" or "160000";

    /// <summary>
    ///     Determines whether two nonzero modes represent the same Git file type.
    /// </summary>
    /// <param name="left">The first six-digit Git mode.</param>
    /// <param name="right">The second six-digit Git mode.</param>
    /// <returns>
    ///     <see langword="true" /> when both modes represent regular files, symbolic links, or gitlinks of the same type.
    /// </returns>
    private static bool IsSameFileType(
        string left,
        string right) =>
        GetFileType(left) == GetFileType(right);

    private static char GetFileType(string mode) =>
        mode switch
        {
            "100644" or "100755" => 'f',
            "120000" => 'l',
            "160000" => 'g',
            _ => '\0',
        };

    private static bool AreCompatibleObjectIds(params string[] values) =>
        values.Length > 0 &&
        values.All(IsSupportedObjectId) &&
        values.All(value => value.Length == values[0].Length);

    private static bool IsSupportedObjectId(string? value) =>
        value is { Length: 40 or 64 } &&
        value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool IsZeroObjectId(string value) =>
        value.All(character => character == '0');

    private static bool IsKnownObjectType(string value) =>
        value is "blob" or "tree" or "commit" or "tag";

    private static bool IsRenameScore(string value)
    {
        if (value.Length is < 2 or > 4 ||
            value[0] != 'R' ||
            value.Length > 2 && value[1] == '0' ||
            !value.AsSpan(1).ToString().All(char.IsAsciiDigit))
        {
            return false;
        }

        var score = int.Parse(value.AsSpan(1), NumberStyles.None, CultureInfo.InvariantCulture);
        return score is >= ComparisonLimits.RenameSimilarityPercent and <= 100;
    }

    private static bool TryParseCanonicalCount(
        string value,
        out int result)
    {
        result = 0;

        if (value.Length == 0 ||
            value.Length > 1 && value[0] == '0' ||
            value.Any(character => !char.IsAsciiDigit(character)))
        {
            return false;
        }

        try
        {
            foreach (var character in value)
            {
                result = checked((result * 10) + (character - '0'));
            }

            return true;
        }
        catch (OverflowException)
        {
            result = 0;
            return false;
        }
    }

    private static bool IsValidFullReference(string value)
    {
        if (!value.StartsWith("refs/", StringComparison.Ordinal) ||
            value.Length == "refs/".Length ||
            value.EndsWith('/') ||
            value.EndsWith('.') ||
            value.Contains("..", StringComparison.Ordinal) ||
            value.Contains("//", StringComparison.Ordinal) ||
            value.Contains("@{", StringComparison.Ordinal) ||
            value.Any(
                character =>
                    char.IsControl(character) ||
                    character is ' ' or '~' or '^' or ':' or '?' or '*' or '[' or '\\'))
        {
            return false;
        }

        var components = value.Split('/');
        return components.All(
                   component =>
                       component.Length > 0 &&
                       !component.StartsWith('.') &&
                       !component.EndsWith(".lock", StringComparison.Ordinal)) &&
               HasValidScalars(value);
    }

    private static bool IsValidOptionalText(string value) =>
        !value.Any(char.IsControl) &&
        HasValidScalars(value);

    private static bool HasValidScalars(string value)
    {
        var position = 0;

        while (position < value.Length)
        {
            var status = Rune.DecodeFromUtf16(
                value.AsSpan(position),
                out _,
                out var consumed);

            if (status != OperationStatus.Done)
            {
                return false;
            }

            position += consumed;
        }

        return true;
    }

    private static string RemoveOneTerminalLineFeed(string value) =>
        value.EndsWith('\n') ? value[..^1] : value;

    private static string? NullWhenEmpty(string value) =>
        value.Length == 0 ? null : value;

    private static Result<T> InspectionFailure<T>() =>
        Result.Fail<T>(
            OperationError.ExternalDependencyFailure(
                "Git comparison inspection failed.",
                ComparisonErrorCode.InspectionFailed));
}
