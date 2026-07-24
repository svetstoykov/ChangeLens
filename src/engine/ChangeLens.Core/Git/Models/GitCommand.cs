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
    /// <param name="maximumStandardOutputBytes">The positive maximum number of bytes captured from standard output.</param>
    /// <param name="maximumStandardErrorBytes">The positive maximum number of bytes captured from standard error.</param>
    /// <param name="errorPolicy">The terminal errors selected by the calling capability. Cannot be <see langword="null" />.</param>
    /// <exception cref="ArgumentNullException">
    ///     <paramref name="arguments" /> is <see langword="null" />.
    ///     -or-
    ///     <paramref name="errorPolicy" /> is <see langword="null" />.
    /// </exception>
    /// <exception cref="ArgumentException">
    ///     <paramref name="arguments" /> contains a <see langword="null" /> value.
    /// </exception>
    /// <exception cref="ArgumentOutOfRangeException">
    ///     <paramref name="timeout" /> is less than or equal to <see cref="TimeSpan.Zero" />.
    ///     -or-
    ///     <paramref name="maximumStandardOutputBytes" /> or <paramref name="maximumStandardErrorBytes" /> is less
    ///     than or equal to zero.
    /// </exception>
    public GitCommand(
        IEnumerable<string> arguments,
        TimeSpan timeout,
        int maximumStandardOutputBytes,
        int maximumStandardErrorBytes,
        GitCommandErrorPolicy errorPolicy)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        var copiedArguments = arguments.ToArray();
        if (copiedArguments.Any(argument => argument is null))
        {
            throw new ArgumentException("Git arguments cannot contain null values.", nameof(arguments));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(timeout, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStandardOutputBytes);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(maximumStandardErrorBytes);
        ArgumentNullException.ThrowIfNull(errorPolicy);
        _arguments = Array.AsReadOnly(copiedArguments);
        Timeout = timeout;
        MaximumStandardOutputBytes = maximumStandardOutputBytes;
        MaximumStandardErrorBytes = maximumStandardErrorBytes;
        ErrorPolicy = errorPolicy;
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
    ///     Gets the maximum number of bytes captured from standard output.
    /// </summary>
    public int MaximumStandardOutputBytes { get; }

    /// <summary>
    ///     Gets the maximum number of bytes captured from standard error.
    /// </summary>
    public int MaximumStandardErrorBytes { get; }

    /// <summary>
    ///     Gets the terminal errors selected by the calling capability.
    /// </summary>
    public GitCommandErrorPolicy ErrorPolicy { get; }
}
