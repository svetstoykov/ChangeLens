using System.Reflection;
using ChangeLens.Engine.EngineInformation.Constants;
using EngineInformationModel = ChangeLens.Engine.EngineInformation.Models.EngineInformation;

namespace ChangeLens.Engine.EngineInformation.Services;

/// <summary>
///     Provides identifying information about the running engine.
/// </summary>
internal sealed class EngineInformationProvider
{
    /// <summary>
    ///     Gets the engine name, assembly version, and supplied protocol version.
    /// </summary>
    /// <param name="protocolVersion">The protocol version supported by the running engine.</param>
    /// <returns>The identifying information for the running engine.</returns>
    internal EngineInformationModel GetInformation(int protocolVersion)
    {
        var version = Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString(EngineInformationConstants.ReportedVersionComponentCount)
            ?? EngineInformationConstants.UnavailableVersion;

        return new EngineInformationModel(
            EngineInformationConstants.EngineName,
            version,
            protocolVersion);
    }
}
