using System.Buffers;
using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.ChangeAnatomy.Helpers;

/// <summary>
///     Tokenizes source text and paths without requiring a language grammar.
/// </summary>
/// <remarks>
///     The tokenizer scans complete file sides so comments and multiline literals carry state across unchanged lines.
///     The caller gates the callback to changed lines; path tokenization passes no line gate.
/// </remarks>
internal sealed class ChangeAnatomyTokenizer
{
    private const int MaximumKeyLength = 120;
    private const int MaximumKeysPerUnit = 4_096;
    private static readonly SearchValues<char> LiteralSeparators = SearchValues.Create("/\\.:-_?&=#@!%$~^|{}[]()<>,;+*\"' \t");
    private readonly int _minimumKeyLength;
    private readonly HashSet<(string Normalized, ChangeAnatomyKeyKind Kind)> _stagedKeys = [];

    /// <summary>
    ///     Initializes a tokenizer with the shortest key it may emit.
    /// </summary>
    /// <param name="minimumKeyLength">The minimum normalized key length. Must be positive.</param>
    internal ChangeAnatomyTokenizer(int minimumKeyLength)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(minimumKeyLength);
        this._minimumKeyLength = minimumKeyLength;
    }

    /// <summary>
    ///     Tokenizes repository path components as path-stem keys.
    /// </summary>
    /// <param name="path">The repository-relative path. Cannot be <see langword="null" />.</param>
    /// <param name="emit">The callback receiving normalized keys, source kind, and no line number. Cannot be <see langword="null" />.</param>
    internal void TokenizePath(string path, Action<string, ChangeAnatomyKeyKind, int?, string> emit)
    {
        ArgumentNullException.ThrowIfNull(path);
        ArgumentNullException.ThrowIfNull(emit);
        this.Reset();
        var remaining = path.AsSpan();
        while (!remaining.IsEmpty)
        {
            var separator = remaining.IndexOfAny('/', '\\');
            var segment = separator < 0 ? StripExtension(remaining) : remaining[..separator];
            remaining = separator < 0 ? ReadOnlySpan<char>.Empty : remaining[(separator + 1)..];
            if (segment.IsEmpty)
            {
                continue;
            }

            this.EmitKey(segment, ChangeAnatomyKeyKind.PathStem, null, emit);
            this.EmitJoined(segment, ChangeAnatomyKeyKind.PathStem, null, emit);
            this.EmitWords(segment, ChangeAnatomyKeyKind.PathStem, ChangeAnatomyKeyKind.PathStem, null, emit);
        }
    }

    /// <summary>
    ///     Tokenizes one source line while carrying lexical state from earlier lines.
    /// </summary>
    /// <param name="line">The source line without its line ending.</param>
    /// <param name="lineNumber">The one-based source line number.</param>
    /// <param name="state">The lexical state to read and update.</param>
    /// <param name="emit">
    ///     The callback receiving normalized keys, source kind, original spelling, and line number. Cannot be
    ///     <see langword="null" />.
    /// </param>
    internal void TokenizeLine(
        ReadOnlySpan<char> line,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(emit);
        cancellationToken.ThrowIfCancellationRequested();
        this.Reset();
        var index = state.Pending == ChangeAnatomyContinuation.None
            ? 0
            : this.Resume(line, lineNumber, ref state, emit, cancellationToken);
        while (index < line.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var current = line[index];
            if (current == '/' && index + 1 < line.Length)
            {
                if (line[index + 1] == '/')
                {
                    this.EmitWords(line[(index + 2)..], ChangeAnatomyKeyKind.CommentWord, ChangeAnatomyKeyKind.CommentWord,
                        lineNumber, emit, cancellationToken);
                    return;
                }

                if (line[index + 1] == '*')
                {
                    index = this.ScanBlockComment(line, index + 2, lineNumber, ref state, emit, cancellationToken);
                    continue;
                }
            }

            if (current == '#' && state.HashStartsComment)
            {
                this.EmitWords(line[(index + 1)..], ChangeAnatomyKeyKind.CommentWord, ChangeAnatomyKeyKind.CommentWord,
                    lineNumber, emit, cancellationToken);
                return;
            }

            if (current == '@' && index + 1 < line.Length && line[index + 1] == '"')
            {
                index = this.ScanVerbatim(line, index + 2, lineNumber, ref state, emit, cancellationToken);
                continue;
            }

            if (current is '"' or '\'' or '`')
            {
                index = this.ScanLiteral(line, index, lineNumber, ref state, emit, cancellationToken);
                continue;
            }

            if (IsIdentifierStart(current))
            {
                var start = index++;
                while (index < line.Length && IsIdentifierPart(line[index]))
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    index++;
                }

                var word = line[start..index];
                this.EmitKey(word, ChangeAnatomyKeyKind.Identifier, lineNumber, emit);
                this.EmitJoined(word, ChangeAnatomyKeyKind.Identifier, lineNumber, emit);
                this.EmitParts(word, ChangeAnatomyKeyKind.IdentifierPart, lineNumber, emit, cancellationToken);
                continue;
            }

            index++;
        }
    }

    private int Resume(
        ReadOnlySpan<char> line,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken) => state.Pending switch
        {
            ChangeAnatomyContinuation.BlockComment => this.ScanBlockComment(line, 0, lineNumber, ref state, emit, cancellationToken),
            ChangeAnatomyContinuation.VerbatimString => this.ScanVerbatim(line, 0, lineNumber, ref state, emit, cancellationToken),
            ChangeAnatomyContinuation.RawString => this.ScanRaw(line, 0, lineNumber, ref state, emit, cancellationToken),
            ChangeAnatomyContinuation.TemplateLiteral => this.ScanTemplate(line, 0, lineNumber, ref state, emit, cancellationToken),
            _ => 0,
        };

    private int ScanLiteral(
        ReadOnlySpan<char> line,
        int index,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        var quote = line[index];
        if (quote == '`')
        {
            return this.ScanTemplate(line, index + 1, lineNumber, ref state, emit, cancellationToken);
        }

        var run = CountRun(line, index, quote);
        if (run >= 3)
        {
            state.RawQuote = quote;
            state.RawQuoteCount = run;
            return this.ScanRaw(line, index + run, lineNumber, ref state, emit, cancellationToken);
        }

        return run == 2 ? index + 2 : this.ScanSingleLine(line, index, quote, lineNumber, emit, cancellationToken);
    }

    private int ScanSingleLine(
        ReadOnlySpan<char> line,
        int start,
        char quote,
        int lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        var index = start + 1;
        while (index < line.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (line[index] == quote)
            {
                this.EmitLiteral(line[(start + 1)..index], lineNumber, emit, cancellationToken);
                return index + 1;
            }

            index++;
        }

        return start + 1;
    }

    private int ScanBlockComment(
        ReadOnlySpan<char> line,
        int start,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        var close = line[start..].IndexOf("*/", StringComparison.Ordinal);
        if (close < 0)
        {
            this.EmitWords(line[start..], ChangeAnatomyKeyKind.CommentWord, ChangeAnatomyKeyKind.CommentWord,
                lineNumber, emit, cancellationToken);
            state.Pending = ChangeAnatomyContinuation.BlockComment;
            return line.Length;
        }

        this.EmitWords(line.Slice(start, close), ChangeAnatomyKeyKind.CommentWord, ChangeAnatomyKeyKind.CommentWord,
            lineNumber, emit, cancellationToken);
        state.Pending = ChangeAnatomyContinuation.None;
        return start + close + 2;
    }

    private int ScanVerbatim(
        ReadOnlySpan<char> line,
        int start,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < line.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line[index] != '"')
            {
                index++;
                continue;
            }

            if (index + 1 < line.Length && line[index + 1] == '"')
            {
                index += 2;
                continue;
            }

            this.EmitLiteral(line[start..index], lineNumber, emit, cancellationToken);
            state.Pending = ChangeAnatomyContinuation.None;
            return index + 1;
        }

        this.EmitLiteral(line[start..], lineNumber, emit, cancellationToken);
        state.Pending = ChangeAnatomyContinuation.VerbatimString;
        return line.Length;
    }

    private int ScanRaw(
        ReadOnlySpan<char> line,
        int start,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        for (var index = start; index < line.Length; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line[index] != state.RawQuote)
            {
                continue;
            }

            var run = CountRun(line, index, state.RawQuote);
            if (run < state.RawQuoteCount)
            {
                index += run - 1;
                continue;
            }

            this.EmitLiteral(line[start..index], lineNumber, emit, cancellationToken);
            state.Pending = ChangeAnatomyContinuation.None;
            state.RawQuoteCount = 0;
            return index + run;
        }

        this.EmitLiteral(line[start..], lineNumber, emit, cancellationToken);
        state.Pending = ChangeAnatomyContinuation.RawString;
        return line.Length;
    }

    private int ScanTemplate(
        ReadOnlySpan<char> line,
        int start,
        int lineNumber,
        ref ChangeAnatomyLexicalState state,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        var index = start;
        while (index < line.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (line[index] == '\\')
            {
                index += 2;
                continue;
            }

            if (line[index] == '`')
            {
                this.EmitLiteral(line[start..index], lineNumber, emit, cancellationToken);
                state.Pending = ChangeAnatomyContinuation.None;
                return index + 1;
            }

            index++;
        }

        this.EmitLiteral(line[start..], lineNumber, emit, cancellationToken);
        state.Pending = ChangeAnatomyContinuation.TemplateLiteral;
        return line.Length;
    }

    private void EmitLiteral(
        ReadOnlySpan<char> value,
        int lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        if (value.IsEmpty)
        {
            return;
        }

        this.EmitKey(value, ChangeAnatomyKeyKind.StringLiteral, lineNumber, emit);
        var remaining = value;
        while (!remaining.IsEmpty)
        {
            var separator = remaining.IndexOfAny(LiteralSeparators);
            if (separator < 0)
            {
                this.EmitKey(remaining, ChangeAnatomyKeyKind.LiteralSegment, lineNumber, emit);
                break;
            }

            if (separator > 0)
            {
                this.EmitKey(remaining[..separator], ChangeAnatomyKeyKind.LiteralSegment, lineNumber, emit);
            }

            remaining = remaining[(separator + 1)..];
        }

        this.EmitWords(value, ChangeAnatomyKeyKind.Identifier, ChangeAnatomyKeyKind.IdentifierPart, lineNumber, emit, cancellationToken);
    }

    private void EmitWords(
        ReadOnlySpan<char> text,
        ChangeAnatomyKeyKind wholeKind,
        ChangeAnatomyKeyKind partKind,
        int? lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken = default)
    {
        var index = 0;
        while (index < text.Length)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsIdentifierStart(text[index]))
            {
                index++;
                continue;
            }

            var start = index++;
            while (index < text.Length && IsIdentifierPart(text[index]))
            {
                index++;
            }

            var word = text[start..index];
            this.EmitKey(word, wholeKind, lineNumber, emit);
            this.EmitJoined(word, wholeKind, lineNumber, emit);
            this.EmitParts(word, partKind, lineNumber, emit, cancellationToken);
        }
    }

    private void EmitParts(
        ReadOnlySpan<char> identifier,
        ChangeAnatomyKeyKind kind,
        int? lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit,
        CancellationToken cancellationToken)
    {
        if (!HasBoundary(identifier))
        {
            return;
        }

        var index = 0;
        while (index < identifier.Length)
        {
            while (index < identifier.Length && IsIdentifierSeparator(identifier[index]))
            {
                index++;
            }

            var start = index;
            while (index < identifier.Length && !IsIdentifierSeparator(identifier[index]))
            {
                cancellationToken.ThrowIfCancellationRequested();
                index++;
            }

            if (index > start)
            {
                this.EmitCasingParts(identifier[start..index], kind, lineNumber, emit);
            }
        }
    }

    private void EmitCasingParts(
        ReadOnlySpan<char> chunk,
        ChangeAnatomyKeyKind kind,
        int? lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit)
    {
        var start = 0;
        for (var index = 1; index < chunk.Length; index++)
        {
            if (!IsCasingBoundary(chunk, index))
            {
                continue;
            }

            this.EmitKey(chunk[start..index], kind, lineNumber, emit);
            start = index;
        }

        if (start < chunk.Length)
        {
            this.EmitKey(chunk[start..], kind, lineNumber, emit);
        }
    }

    private void EmitKey(
        ReadOnlySpan<char> raw,
        ChangeAnatomyKeyKind kind,
        int? lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit)
    {
        raw = raw.Trim();
        if (raw.Length < this._minimumKeyLength || raw.Length > MaximumKeyLength || this._stagedKeys.Count >= MaximumKeysPerUnit)
        {
            return;
        }

        var original = raw.ToString();
        var normalized = original.ToLowerInvariant();
        if (!ContainsLetter(normalized) || !this._stagedKeys.Add((normalized, kind)))
        {
            return;
        }

        emit(normalized, kind, lineNumber, original);
    }

    private void EmitJoined(
        ReadOnlySpan<char> raw,
        ChangeAnatomyKeyKind kind,
        int? lineNumber,
        Action<string, ChangeAnatomyKeyKind, int?, string> emit)
    {
        raw = raw.Trim();
        if (!HasSeparator(raw) || raw.Length > MaximumKeyLength || this._stagedKeys.Count >= MaximumKeysPerUnit)
        {
            return;
        }

        Span<char> joined = stackalloc char[MaximumKeyLength];
        var written = 0;
        foreach (var character in raw)
        {
            if (char.IsLetterOrDigit(character))
            {
                joined[written++] = char.ToLowerInvariant(character);
            }
        }

        if (written < this._minimumKeyLength)
        {
            return;
        }

        var normalized = joined[..written].ToString();
        if (!ContainsLetter(normalized) || !this._stagedKeys.Add((normalized, kind)))
        {
            return;
        }

        emit(normalized, kind, lineNumber, raw.ToString());
    }

    private void Reset() => this._stagedKeys.Clear();

    private static ReadOnlySpan<char> StripExtension(ReadOnlySpan<char> name)
    {
        var dot = name.LastIndexOf('.');
        return dot > 0 ? name[..dot] : name;
    }

    private static int CountRun(ReadOnlySpan<char> line, int start, char quote)
    {
        var index = start;
        while (index < line.Length && line[index] == quote)
        {
            index++;
        }

        return index - start;
    }

    private static bool HasBoundary(ReadOnlySpan<char> identifier)
    {
        for (var index = 0; index < identifier.Length; index++)
        {
            if (IsIdentifierSeparator(identifier[index]) || index > 0 && IsCasingBoundary(identifier, index))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsCasingBoundary(ReadOnlySpan<char> chunk, int index) =>
        char.IsUpper(chunk[index]) && (!char.IsUpper(chunk[index - 1])
            || index + 1 < chunk.Length && char.IsLower(chunk[index + 1]));

    private static bool HasSeparator(ReadOnlySpan<char> value)
    {
        foreach (var character in value)
        {
            if (!char.IsLetterOrDigit(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsLetter(string value)
    {
        foreach (var character in value)
        {
            if (char.IsLetter(character))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsIdentifierSeparator(char character) => character is '_' or '$';

    private static bool IsIdentifierStart(char character) =>
        char.IsAsciiLetter(character) || character is '_' or '$' || character > 127 && char.IsLetter(character);

    private static bool IsIdentifierPart(char character) =>
        char.IsAsciiLetterOrDigit(character) || character is '_' or '$' || character > 127 && char.IsLetterOrDigit(character);
}
