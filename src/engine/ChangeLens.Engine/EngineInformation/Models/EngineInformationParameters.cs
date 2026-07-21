using System.Text.Json.Serialization;

namespace ChangeLens.Engine.EngineInformation.Models;

/// <summary>
///     Represents the parameters accepted by the engine information action.
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
internal sealed class EngineInformationParameters
{
}
