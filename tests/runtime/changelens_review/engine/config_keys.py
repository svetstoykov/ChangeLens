"""Engine settings a plan may override, and the ones the harness owns."""

type ConfigValue = bool | int | float | str

_SECTIONS: dict[str, tuple[str, ...]] = {
    "Analysis.ChangeAnatomy": ("MinimumKeyLength", "MaximumKeysPerFile", "MaximumOccurrencesPerKey"),
    "Analysis.ContextPolicy": (
        "MaximumDisclosedCharactersPerNode",
        "MaximumRedactedLineShare",
        "MinimumSecretValueLength",
    ),
    "Analysis.Curator": ("MaximumOutputCharacters", "MaximumOutputTokens", "ReasoningEffort"),
    "Snapshots.FrozenGitTree": (
        "MaximumBlobBytes",
        "MaximumTreeFiles",
        "MaximumHistoryCommits",
        "MaximumHistoryPathsPerCommit",
        "CommandTimeout",
    ),
    "Analysis.EvidenceGraph": (
        "MaximumQuoteWindowNodes",
        "ChangedNodeShare",
        "HunkContextLines",
        "MaximumQuotedLinesPerNode",
        "CandidateWindowLines",
        "MergeGapLines",
        "MaximumNodesPerChangedFile",
        "MaximumNodesPerCandidate",
        "MaximumCandidateHeadNodes",
        "MaximumEdges",
        "MaximumEdgesPerNodePair",
    ),
    "Analysis.Frontier": ("MaximumEntries",),
    "Analysis.EvidenceBinder": (
        "ContextWindowTokens",
        "CuratorOutputCharacters",
        "PromptReserveCharacters",
        "MaximumBinderCharacters",
        "TargetUtilization",
        "MaximumOrientationPathsPerDirectory",
        "MaximumOrientationPaths",
        "MaximumDeveloperContextCharacters",
        "MaximumOmissionItems",
        "MaximumOmissionItemCharacters",
        "MaximumTracks",
        "MaximumParticipantsPerTrack",
        "MaximumRelationshipsPerTrack",
        "MaximumItemsPerTrack",
        "MaximumStatementCharacters",
    ),
    "Analysis.Checker": (
        "Enabled",
        "MaximumClaims",
        "MaximumPayloadCharacters",
        "Attempts",
        "ReasoningEffort",
        "MaximumOutputTokens",
    ),
    "Analysis.Correspondence": (
        "MaximumCandidates",
        "MaximumIndexedKeysPerFile",
        "MaximumReasonsPerCandidate",
        "IncludeCoChange",
        "IdentifierWeight",
        "IdentifierPartWeight",
        "StringLiteralWeight",
        "LiteralSegmentWeight",
        "PathStemWeight",
        "CommentWordWeight",
        "CrossLanguageBoost",
        "CoChangeWeight",
        "MaximumCoChangeContributionPerPair",
        "MaximumCoChangeContributionPerCandidate",
        "CoChangeHalfLifeInCommits",
        "CommonKeyFileRatio",
        "CommonKeyMinimumFileCount",
        "MaximumDominantSignalShare",
    ),
    "Analysis.ModelCompletion": ("RequestTimeout", "MaximumResponseBytes"),
}

RESERVED_KEYS = frozenset(
    {
        "LocalState.Directory",
        "Logging.FileDirectory",
        "Analysis.ModelCompletion.BaseUrl",
        "Analysis.ModelCompletion.Model",
        "Analysis.ModelCompletion.ApiKey",
    }
)
CONFIGURABLE_KEYS = frozenset(
    {f"{section}.{name}" for section, names in _SECTIONS.items() for name in names} | {"Repositories.GitExecutable"}
)


def environment_name(key: str) -> str:
    """Return the environment variable that overrides a dotted ChangeLens setting."""
    return "ChangeLens__" + key.replace(".", "__")


def environment_value(value: ConfigValue) -> str:
    """Return a setting value in the form .NET configuration binding expects."""
    if isinstance(value, bool):
        return "true" if value else "false"
    return str(value)
