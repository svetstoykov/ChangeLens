using ChangeLens.Core.ChangeAnatomy.Models;
using ChangeLens.Core.Correspondence.Models;

namespace ChangeLens.Core.Correspondence.Helpers;

/// <summary>
///     Collects the bounded, de-duplicated normalized keys indexed for one captured tree file.
/// </summary>
internal sealed class CorrespondenceIndexedKeyCollector
{
    private readonly Dictionary<string, CorrespondenceIndexedToken> _tokens = new(StringComparer.Ordinal);
    private readonly int _maximumKeys;
    private readonly int _maximumLines;

    /// <summary>
    ///     Initializes a bounded indexed-key collector.
    /// </summary>
    /// <param name="maximumKeys">The maximum distinct keys to retain. Must be positive.</param>
    /// <param name="maximumOccurrencesPerKey">The maximum distinct lines to retain per key. Must be positive.</param>
    internal CorrespondenceIndexedKeyCollector(int maximumKeys, int maximumOccurrencesPerKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumKeys);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOccurrencesPerKey);
        this._maximumKeys = maximumKeys;
        this._maximumLines = maximumOccurrencesPerKey;
    }

    /// <summary>
    ///     Gets a value indicating whether a new distinct key was omitted by the configured per-file cap.
    /// </summary>
    internal bool Truncated { get; private set; }

    /// <summary>
    ///     Adds one emitted key unless the per-file distinct-key cap has been reached.
    /// </summary>
    /// <param name="normalized">The lower-case normalized key. Cannot be <see langword="null" />.</param>
    /// <param name="kind">The key kind.</param>
    /// <param name="lineNumber">The one-based source line, or <see langword="null" /> for a path key.</param>
    /// <param name="original">The original spelling retained by the tokenizer.</param>
    internal void Add(string normalized, ChangeAnatomyKeyKind kind, int? lineNumber, string original)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        if (!this._tokens.TryGetValue(normalized, out var token))
        {
            if (this._tokens.Count >= this._maximumKeys)
            {
                this.Truncated = true;
                return;
            }

            token = new CorrespondenceIndexedToken(normalized);
            this._tokens.Add(normalized, token);
        }

        token.AddKind(kind);
        if (lineNumber is not null)
        {
            token.AddLine(lineNumber.Value, this._maximumLines);
        }
    }

    /// <summary>
    ///     Builds the indexed tokens keyed by normalized value.
    /// </summary>
    /// <returns>The indexed tokens in first-seen key order.</returns>
    internal IReadOnlyDictionary<string, CorrespondenceIndexedToken> Build() => this._tokens;
}
