using System.Reflection;
using EngineInformationModel = ChangeLens.Engine.EngineInformation.Models.EngineInformation;

namespace ChangeLens.Engine.EngineInformation.Services;

internal sealed class EngineInformationProvider
{
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
