namespace ChangeLens.Core.ChangeAnatomy.Services;

/// <summary>
///     Represents lexical state carried while scanning one captured file side.
/// </summary>
internal struct ChangeAnatomyLexicalState
{
    /// <summary>
    ///     Gets or sets the construct that remains open at the end of the previous line.
    /// </summary>
    internal ChangeAnatomyContinuation Pending { get; set; }

    /// <summary>
    ///     Gets or sets the quote character for an open raw string.
    /// </summary>
    internal char RawQuote { get; set; }

    /// <summary>
    ///     Gets or sets the number of quote characters required to close an open raw string.
    /// </summary>
    internal int RawQuoteCount { get; set; }

    /// <summary>
    ///     Gets or sets a value indicating whether hash starts a comment for this file.
    /// </summary>
    internal bool HashStartsComment { get; set; }

    /// <summary>
    ///     Creates initial lexical state based on the file path.
    /// </summary>
    /// <param name="path">The repository-relative file path. Cannot be <see langword="null" />.</param>
    /// <returns>The initial state for the file.</returns>
    internal static ChangeAnatomyLexicalState ForPath(string path)
    {
        ArgumentNullException.ThrowIfNull(path);
        var slash = path.LastIndexOfAny(['/', '\\']);
        var name = slash < 0 ? path.AsSpan() : path.AsSpan(slash + 1);
        var dot = name.LastIndexOf('.');
        var extension = dot > 0 ? name[(dot + 1)..] : ReadOnlySpan<char>.Empty;
        var hashStartsComment = extension.Equals("py", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("rb", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("sh", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("bash", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("ps1", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("pl", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("yml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("yaml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("toml", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("tf", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("gitignore", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("dockerignore", StringComparison.OrdinalIgnoreCase)
            || extension.Equals("editorconfig", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".gitignore", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".dockerignore", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".editorconfig", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Dockerfile", StringComparison.OrdinalIgnoreCase)
            || name.Equals("Makefile", StringComparison.OrdinalIgnoreCase)
            || name.Equals(".env", StringComparison.OrdinalIgnoreCase);
        return new ChangeAnatomyLexicalState { HashStartsComment = hashStartsComment };
    }
}
