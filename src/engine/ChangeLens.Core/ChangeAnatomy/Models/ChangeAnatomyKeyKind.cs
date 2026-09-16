namespace ChangeLens.Core.ChangeAnatomy.Models;

/// <summary>
///     Defines the source forms from which a change anatomy key was extracted.
/// </summary>
public enum ChangeAnatomyKeyKind
{
    /// <summary>
    ///     An identifier-shaped run from source text.
    /// </summary>
    Identifier,

    /// <summary>
    ///     A word or casing part of a compound identifier.
    /// </summary>
    IdentifierPart,

    /// <summary>
    ///     The complete value inside a quoted literal.
    /// </summary>
    StringLiteral,

    /// <summary>
    ///     A segment of a quoted literal separated by punctuation.
    /// </summary>
    LiteralSegment,

    /// <summary>
    ///     A repository path component with its extension removed where applicable.
    /// </summary>
    PathStem,

    /// <summary>
    ///     An identifier-shaped word in a comment.
    /// </summary>
    CommentWord,
}
