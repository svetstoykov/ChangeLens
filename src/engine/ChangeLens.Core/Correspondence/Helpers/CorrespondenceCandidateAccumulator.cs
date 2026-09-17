using ChangeLens.Core.Correspondence.Models;

namespace ChangeLens.Core.Correspondence.Helpers;

/// <summary>
///     Accumulates the scored signals for one correspondence candidate and builds its record.
/// </summary>
internal sealed class CorrespondenceCandidateAccumulator
{
    private readonly CorrespondenceIndexedFile _file;
    private readonly List<CorrespondenceSignal> _signals = [];
    private readonly Dictionary<CorrespondenceSignalKind, double> _familyScores = [];
    private readonly HashSet<string> _changedPaths = new(StringComparer.Ordinal);
    private double _score;

    /// <summary>
    ///     Initializes an accumulator for one indexed candidate file.
    /// </summary>
    /// <param name="file">The indexed file the candidate represents. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="file" /> is <see langword="null" />.</exception>
    internal CorrespondenceCandidateAccumulator(CorrespondenceIndexedFile file)
    {
        ArgumentNullException.ThrowIfNull(file);
        this._file = file;
    }

    /// <summary>
    ///     Adds one signal, ignoring contributions that are not positive.
    /// </summary>
    /// <param name="signal">The scored signal to add. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException"><paramref name="signal" /> is <see langword="null" />.</exception>
    internal void Add(CorrespondenceSignal signal)
    {
        ArgumentNullException.ThrowIfNull(signal);
        if (signal.Contribution <= 0)
        {
            return;
        }

        this._signals.Add(signal);
        this._changedPaths.Add(signal.ChangedPath);
        this._score += signal.Contribution;
        if (signal.Kind is not CorrespondenceSignalKind.CrossLanguage)
        {
            this._familyScores[signal.Kind] = this._familyScores.GetValueOrDefault(signal.Kind) + signal.Contribution;
        }
    }

    /// <summary>
    ///     Builds the candidate record with an unranked position.
    /// </summary>
    /// <param name="maximumReasons">The maximum number of leading signals to keep as display reasons.</param>
    /// <returns>The built candidate with rank zero.</returns>
    internal CorrespondenceCandidate Build(int maximumReasons)
    {
        var ordered = this._signals
            .OrderBy(signal => signal.Kind is CorrespondenceSignalKind.CrossLanguage)
            .ThenByDescending(signal => signal.Contribution)
            .ThenBy(signal => signal.Kind)
            .ThenBy(MatchedValue, StringComparer.Ordinal)
            .ThenBy(signal => signal.ChangedPath, StringComparer.Ordinal)
            .ToArray();
        var dominant = this._familyScores
            .OrderByDescending(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .Select(pair => pair.Key)
            .FirstOrDefault(CorrespondenceSignalKind.SharedIdentifier);
        var changedPaths = this._changedPaths.Order(StringComparer.Ordinal).ToArray();
        var reasons = ordered.Take(maximumReasons).ToArray();
        var score = Math.Round(this._score, 3);
        return new CorrespondenceCandidate(0, this._file.Path, this._file.ObjectId, score, dominant, changedPaths, ordered, reasons);
    }

    private static string MatchedValue(CorrespondenceSignal signal) =>
        signal is SharedKeyCorrespondenceSignal shared ? shared.MatchedValue : string.Empty;
}
