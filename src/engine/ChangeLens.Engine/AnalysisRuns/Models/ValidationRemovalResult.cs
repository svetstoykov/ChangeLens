namespace ChangeLens.Engine.AnalysisRuns.Models;

/// <summary>
///     Represents one draft item mechanical validation removed before publication.
/// </summary>
/// <param name="Scope">The removal scope wire value.</param>
/// <param name="Id">The identifier of the removed item.</param>
/// <param name="Reason">The producer reason for the removal.</param>
internal sealed record ValidationRemovalResult(string Scope, string Id, string Reason);
