namespace ChangeLens.Core.ChangeAnatomy.Services;

/// <summary>
///     Defines lexical constructs that continue across source lines.
/// </summary>
internal enum ChangeAnatomyContinuation
{
    /// <summary>
    ///     No construct is pending.
    /// </summary>
    None,

    /// <summary>
    ///     A block comment is open.
    /// </summary>
    BlockComment,

    /// <summary>
    ///     A verbatim double-quoted string is open.
    /// </summary>
    VerbatimString,

    /// <summary>
    ///     A raw quoted string is open.
    /// </summary>
    RawString,

    /// <summary>
    ///     A backtick template literal is open.
    /// </summary>
    TemplateLiteral,
}
