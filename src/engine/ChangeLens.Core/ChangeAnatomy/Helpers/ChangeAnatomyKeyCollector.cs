using ChangeLens.Core.ChangeAnatomy.Models;

namespace ChangeLens.Core.ChangeAnatomy.Helpers;

/// <summary>
///     Accumulates bounded, de-duplicated keys for one changed file.
/// </summary>
internal sealed class ChangeAnatomyKeyCollector
{
    private readonly Dictionary<(string Normalized, ChangeAnatomyKeyKind Kind), List<ChangeAnatomyKeyOccurrence>> _keys = [];
    private readonly int _maximumKeys;
    private readonly int _maximumOccurrencesPerKey;

    /// <summary>
    ///     Initializes a bounded key collector.
    /// </summary>
    /// <param name="maximumKeys">The maximum distinct keys to retain. Must be positive.</param>
    /// <param name="maximumOccurrencesPerKey">The maximum occurrences to retain per key. Must be positive.</param>
    internal ChangeAnatomyKeyCollector(int maximumKeys, int maximumOccurrencesPerKey)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumKeys);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumOccurrencesPerKey);
        this._maximumKeys = maximumKeys;
        this._maximumOccurrencesPerKey = maximumOccurrencesPerKey;
    }

    /// <summary>
    ///     Gets a value indicating whether a distinct key was omitted by the configured cap.
    /// </summary>
    internal bool Truncated { get; private set; }

    /// <summary>
    ///     Adds one key occurrence unless the key or occurrence bound has been reached.
    /// </summary>
    /// <param name="normalized">The normalized key. Cannot be <see langword="null" />.</param>
    /// <param name="kind">The key source kind.</param>
    /// <param name="side">The comparison side.</param>
    /// <param name="lineNumber">The changed line number, or <see langword="null" /> for a path key.</param>
    /// <param name="original">The original spelling. Cannot be <see langword="null" />.</param>
    internal void Add(string normalized, ChangeAnatomyKeyKind kind, ChangeAnatomySide side, int? lineNumber, string original)
    {
        ArgumentNullException.ThrowIfNull(normalized);
        ArgumentNullException.ThrowIfNull(original);
        var identity = (normalized, kind);
        if (!this._keys.TryGetValue(identity, out var occurrences))
        {
            if (this._keys.Count >= this._maximumKeys)
            {
                this.Truncated = true;
                return;
            }

            occurrences = [];
            this._keys.Add(identity, occurrences);
        }

        if (occurrences.Count >= this._maximumOccurrencesPerKey)
        {
            return;
        }

        var occurrence = new ChangeAnatomyKeyOccurrence(side, lineNumber, original);
        if (!occurrences.Contains(occurrence))
        {
            occurrences.Add(occurrence);
        }
    }

    /// <summary>
    ///     Builds the stable output order for the collected keys.
    /// </summary>
    /// <returns>The collected keys ordered by occurrence count and normalized spelling.</returns>
    internal IReadOnlyList<ChangeAnatomyKey> Build() => this._keys
        .Select(pair => new ChangeAnatomyKey(pair.Key.Normalized, pair.Key.Kind, pair.Value))
        .OrderByDescending(key => key.Occurrences.Count)
        .ThenBy(key => key.Normalized, StringComparer.Ordinal)
        .ThenBy(key => key.Kind)
        .ToArray();
}
