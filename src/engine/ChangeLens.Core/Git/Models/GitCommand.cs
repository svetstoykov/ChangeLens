using System.Collections.ObjectModel;

namespace ChangeLens.Core.Git.Models;

/// <summary>
///     Represents an immutable request to run Git with bounded execution resources.
/// </summary>
public sealed class GitCommand
{
    private readonly ReadOnlyCollection<string> _arguments;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GitCommand" /> class.
    /// </summary>
    /// <param name="arguments">
    ///     The Git argument sequence, excluding the executable name. Cannot be <see langword="null" /> or contain
    ///     <see langword="null" /> values.
    /// </param>
    /// <param name="timeout">The positive time allowed for the process to complete.</param>
    /// <param name="maximumStreamBytes">The positive maximum number of bytes captured from each output stream.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="arguments" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="arguments" /> contains a <see langword="null" /> value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />.
    ///     -or-
    ///     <paramref name="maximumStreamBytes" /> is less than or equal to zero.
    /// </exception>
    public GitCommand(
        IEnumerable<string> arguments,
        TimeSpan timeout,
        int maximumStreamBytes)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var copiedArguments = arguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
        {
            throw new ArgumentException("Git arguments cannot contain null values.", nameof(arguments));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStreamBytes);
        _arguments = Array.AsReadOnly(copiedArguments);
        Timeout = timeout;
        MaximumStreamBytes = maximumStreamBytes;
    }

    /// <summary>
    ///     Gets the copied, read-only Git argument sequence.
    /// </summary>
    public IReadOnlyList<string> Arguments => _arguments;

    /// <summary>
    ///     Gets the time allowed for the process to complete.
    /// </summary>
    public TimeSpan Timeout { get; }

    /// <summary>
    ///     Gets the maximum number of bytes captured from each output stream.
    /// </summary>
    public int MaximumStreamBytes { get; }
}
