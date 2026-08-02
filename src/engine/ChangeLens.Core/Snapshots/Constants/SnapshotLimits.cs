namespace ChangeLens.Core.Snapshots.Constants;

/// <summary>
///     Provides product-owned bounds for one snapshot capture.
/// </summary>
internal static class SnapshotLimits
{
    /// <summary>Gets the total budget shared by every Git call in one capture.</summary>
    internal static readonly TimeSpan CaptureTimeout = TimeSpan.FromSeconds(60);

    /// <summary>Defines the maximum standard-output size for a capture command.</summary>
    internal const int MaximumCaptureOutputBytes = 8 * 1024 * 1024;

    /// <summary>Defines the maximum standard-error size for a capture command.</summary>
    internal const int MaximumDiagnosticBytes = 64 * 1024;

    /// <summary>Defines the maximum number of entries one manifest may carry.</summary>
    internal const int MaximumManifestEntries = 25_000;
}
