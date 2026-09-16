namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Defines the side of a captured comparison where a key occurred.
/// </summary>
public enum ChangeAnatomySide
{
    /// <summary>
    ///     The merge-base side of the comparison.
    /// </summary>
    Before,

    /// <summary>
    ///     The HEAD side of the comparison.
    /// </summary>
    After,
}
