namespace ChangeLens.Core.Publication.Models;

/// <summary>Summarizes one bounded binder omission group.</summary>
/// <param name="SourceKind">The binder omission source kind.</param>
/// <param name="Reason">The binder omission reason.</param>
/// <param name="TotalCount">The full source item count.</param>
/// <param name="SampleCount">The number of sampled source items.</param>
/// <param name="ResolvedSampleCount">The number of sampled items resolved exactly.</param>
public sealed record ReadingOmissionSummary(string SourceKind, string Reason, int TotalCount, int SampleCount, int ResolvedSampleCount);
