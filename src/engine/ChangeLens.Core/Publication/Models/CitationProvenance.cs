namespace ChangeLens.Core.Publication.Models;

/// <summary>Identifies the source of trust for one citation.</summary>
public enum CitationProvenance
{
    /// <summary>The claim was retained without checker support.</summary>
    Unchecked,

    /// <summary>The checker supported the claim.</summary>
    Checked,

    /// <summary>The statement was derived from checked claims.</summary>
    Derived,
}
