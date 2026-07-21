using System.Reflection;
using ChangeLens.Engine.EngineInformation.Constants;
using ChangeLens.Engine.EngineInformation.Models;
using EngineInformationModel = ChangeLens.Engine.EngineInformation.Models.EngineInformation;

namespace ChangeLens.Engine.EngineInformation.Services;

/// <summary>
///     Provides identifying information about the running engine.
/// </summary>
/// <remarks>
///     The host registers this stateless service as a singleton. It is safe to use concurrently.
/// </remarks>
internal sealed class EngineInformationProvider
{
    /// <summary>
    ///     Gets the engine name, assembly version, and supplied protocol version.
    /// </summary>
    /// <param name="parameters">The action parameters. Cannot be <see langword="null" />.</param>
    /// <param name="protocolVersion">The protocol version supported by the running engine.</param>
    /// <returns>The identifying information for the running engine.</returns>
    internal EngineInformationModel GetInformation(
        EngineInformationParameters parameters,
        int protocolVersion)
    {
        ArgumentNullException.ThrowIfNull(parameters);

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
