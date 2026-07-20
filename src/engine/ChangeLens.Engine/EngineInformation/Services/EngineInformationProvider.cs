using System.Reflection;
using EngineInformationModel = ChangeLens.Engine.EngineInformation.Models.EngineInformation;

namespace ChangeLens.Engine.EngineInformation.Services;

/// <summary>
///     Provides identifying information about the running engine.
/// </summary>
internal sealed class EngineInformationProvider
{
    /// <summary>
    ///     Gets the engine name, assembly version, and supported protocol version.
    /// </summary>
    /// <returns>The identifying information for the running engine.</returns>
    internal EngineInformationModel GetInformation()
    {
        var version = Assembly
            .GetExecutingAssembly()
            .GetName()
            .Version?
            .ToString(3) ?? "0.0.0";

        return new EngineInformationModel("ChangeLens.Engine", version, 1);
    }
}
