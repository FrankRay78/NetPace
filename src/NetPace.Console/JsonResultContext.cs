using System.Text.Json.Serialization;

namespace NetPace.Console;

[JsonSerializable(typeof(JsonResult))]
[JsonSourceGenerationOptions(WriteIndented = false, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class JsonResultCompactContext : JsonSerializerContext
{
}

[JsonSerializable(typeof(JsonResult))]
[JsonSourceGenerationOptions(WriteIndented = true, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
internal sealed partial class JsonResultIndentedContext : JsonSerializerContext
{
}
