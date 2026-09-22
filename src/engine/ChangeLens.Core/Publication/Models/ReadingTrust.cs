namespace ChangeLens.Core.Publication.Models;

/// <summary>Describes the strongest trust level attached to a published claim.</summary>
public enum ReadingTrust
{
    /// <summary>No checker verdict supports the claim.</summary>
    Unchecked,

    /// <summary>The claim is derived from checked claims.</summary>
    Derived,

    /// <summary>The checker supports the claim.</summary>
    Checked,
}
