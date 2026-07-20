namespace ChangeLens.Engine.EngineInformation.Constants;

/// <summary>
///     Provides stable values used to describe the ChangeLens engine.
/// </summary>
internal static class EngineInformationConstants
{
    /// <summary>
    ///     The stable name of the engine executable.
    /// </summary>
    internal const string EngineName = "ChangeLens.Engine";

    /// <summary>
    ///     The version reported when the engine assembly has no version metadata.
    /// </summary>
    internal const string UnavailableVersion = "0.0.0";

    /// <summary>
    ///     The number of version components exposed through engine information.
    /// </summary>
    internal const int ReportedVersionComponentCount = 3;
}
