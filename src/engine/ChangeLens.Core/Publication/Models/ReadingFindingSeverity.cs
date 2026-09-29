namespace ChangeLens.Core.Publication.Models;

/// <summary>Defines the published severity of one finding, most severe first.</summary>
public enum ReadingFindingSeverity
{
    /// <summary>A demonstrated high-impact defect.</summary>
    Critical,

    /// <summary>An actionable bug or regression with meaningful but lower impact.</summary>
    Warning,

    /// <summary>A small, concrete, useful improvement.</summary>
    Info,
}
