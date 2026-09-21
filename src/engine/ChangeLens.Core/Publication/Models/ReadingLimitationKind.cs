namespace ChangeLens.Core.Publication.Models;

/// <summary>Defines a published limitation category.</summary>
public enum ReadingLimitationKind
{
    /// <summary>A changed file was not read.</summary>
    FileNotRead,

    /// <summary>A changed file or evidence node was not quoted.</summary>
    FileNotQuoted,

    /// <summary>Uncommitted work was outside the comparison.</summary>
    UncommittedWorkExcluded,
}
