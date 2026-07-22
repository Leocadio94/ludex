using System.Text.Json.Serialization;

namespace Flow.Launcher.Plugin.GameSavesManager.Lib;

/// <summary>
/// Source-generated serialization for the two files this plugin writes. Reflection-based
/// serialization of a ~19k entry dictionary is measurably slower on every cold start.
/// </summary>
[JsonSourceGenerationOptions(DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(Dictionary<string, IndexedGame>))]
[JsonSerializable(typeof(IndexedGame))]
[JsonSerializable(typeof(List<InstalledGame>))]
internal partial class JsonContext : JsonSerializerContext
{
}
