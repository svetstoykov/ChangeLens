namespace ChangeLens.Core.ClaimChecking.Models;

/// <summary>
///     Represents claim-checking configuration.
/// </summary>
public sealed class ClaimCheckingOptions
{
    /// <summary>
    ///     Gets or sets a value indicating whether checking is enabled. The default is <see langword="false" />.
    /// </summary>
    public bool Enabled { get; set; }
}
