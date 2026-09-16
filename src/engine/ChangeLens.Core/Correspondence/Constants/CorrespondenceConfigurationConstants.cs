namespace ChangeLens.Core.Correspondence.Constants;

/// <summary>
///     Defines configuration keys for correspondence ranking.
/// </summary>
public static class CorrespondenceConfigurationConstants
{
    /// <summary>
    ///     The configuration section containing correspondence settings.
    /// </summary>
    public const string SectionKey = "ChangeLens:Analysis:Correspondence";

    /// <summary>
    ///     The configuration key for the maximum returned candidates.
    /// </summary>
    public const string MaximumCandidatesKey = SectionKey + ":MaximumCandidates";

    /// <summary>
    ///     The configuration key for the maximum distinct keys indexed per tree file.
    /// </summary>
    public const string MaximumIndexedKeysPerFileKey = SectionKey + ":MaximumIndexedKeysPerFile";

    /// <summary>
    ///     The configuration key for the maximum display reasons per candidate.
    /// </summary>
    public const string MaximumReasonsPerCandidateKey = SectionKey + ":MaximumReasonsPerCandidate";

    /// <summary>
    ///     The configuration key that enables or disables co-change history signals.
    /// </summary>
    public const string IncludeCoChangeKey = SectionKey + ":IncludeCoChange";
}
