namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Represents deterministic keys extracted from every entry in a captured change manifest.
/// </summary>
/// <param name="Files">The per-entry anatomy outcomes in manifest order. Cannot be <see langword="null" />.</param>
/// <param name="Diagnostics">The aggregate extraction diagnostics. Cannot be <see langword="null" />.</param>
public sealed record ChangeAnatomy(
    IReadOnlyList<ChangedFileAnatomy> Files,
    ChangeAnatomyDiagnostics Diagnostics);
