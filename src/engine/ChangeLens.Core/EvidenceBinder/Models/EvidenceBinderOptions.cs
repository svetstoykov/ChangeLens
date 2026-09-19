namespace ChangeLens.Core.EvidenceBinder.Models;

/// <summary>
///     Represents configurable limits for evidence binder assembly.
/// </summary>
/// <remarks>
///     Capacity is derived from the declared context window at 3.25 characters per token, then reduced by the curator
///     output and prompt reserves. An explicit maximum is available for controlled fixtures and deployment policy.
/// </remarks>
public sealed class EvidenceBinderOptions
{
    /// <summary>
    ///     Gets or sets the declared model context window in tokens. The default is 128,000 tokens.
    /// </summary>
    public int ContextWindowTokens { get; set; } = 128_000;

    /// <summary>
    ///     Gets or sets the curator output reserve in characters. The default is 176,000 characters, matching the
    ///     prototype's 228,000-character binder cap at a 128,000-token context window.
    /// </summary>
    public int CuratorOutputCharacters { get; set; } = 176_000;

    /// <summary>
    ///     Gets or sets the curator prompt reserve in characters. The default is 16,000 characters, which covers the
    ///     complete curator instructions and worked examples.
    /// </summary>
    public int PromptReserveCharacters { get; set; } = 16_000;

    /// <summary>
    ///     Gets or sets an optional hard binder cap in characters. Null derives the cap from the context window.
    /// </summary>
    public int? MaximumBinderCharacters { get; set; }

    /// <summary>
    ///     Gets or sets the fraction of the hard cap used as the ladder target. The default is 0.75.
    /// </summary>
    public double TargetUtilization { get; set; } = 0.75;

    /// <summary>Gets or sets the maximum sibling paths retained per directory. The default is 12.</summary>
    public int MaximumOrientationPathsPerDirectory { get; set; } = 12;

    /// <summary>Gets or sets the maximum sibling paths retained overall. The default is 200.</summary>
    public int MaximumOrientationPaths { get; set; } = 200;

    /// <summary>Gets or sets the maximum developer-context characters. The default is 2,000.</summary>
    public int MaximumDeveloperContextCharacters { get; set; } = 2_000;

    /// <summary>Gets or sets the maximum omission items per group. The default is 200.</summary>
    public int MaximumOmissionItems { get; set; } = 200;

    /// <summary>Gets or sets the maximum characters in one omission item. The default is 256.</summary>
    public int MaximumOmissionItemCharacters { get; set; } = 256;

    /// <summary>Gets or sets the curator track limit. The default is 20.</summary>
    public int MaximumTracks { get; set; } = 20;

    /// <summary>Gets or sets the participant limit per track. The default is 40.</summary>
    public int MaximumParticipantsPerTrack { get; set; } = 40;

    /// <summary>Gets or sets the relationship limit per track. The default is 80.</summary>
    public int MaximumRelationshipsPerTrack { get; set; } = 80;

    /// <summary>Gets or sets the ordered-step and purpose limit per track. The default is 40.</summary>
    public int MaximumItemsPerTrack { get; set; } = 40;

    /// <summary>Gets or sets the maximum characters in a curator statement. The default is 1,000.</summary>
    public int MaximumStatementCharacters { get; set; } = 1_000;

    /// <summary>
    ///     Gets the hard character cap derived from the configured window and reserves.
    /// </summary>
    public int EffectiveMaximumBinderCharacters => this.MaximumBinderCharacters
        ?? Math.Max(1, (int)Math.Floor(this.ContextWindowTokens * 3.25) - this.CuratorOutputCharacters - this.PromptReserveCharacters);

    /// <summary>
    ///     Gets the character target used before the hard-cap safety check.
    /// </summary>
    public int TargetBinderCharacters => Math.Max(1, (int)Math.Floor(this.EffectiveMaximumBinderCharacters * this.TargetUtilization));
}
