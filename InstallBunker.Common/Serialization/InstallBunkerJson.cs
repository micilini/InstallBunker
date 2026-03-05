using System.Text.Json;
using System.Text.Json.Serialization;

namespace InstallBunker.Common.Serialization;

public static class InstallBunkerJson
{
    public static readonly JsonSerializerOptions DefaultOptions = CreateDefaultOptions();

    public static string Serialize<T>(T value)
    {
        return JsonSerializer.Serialize(value, DefaultOptions);
    }

    public static T? Deserialize<T>(string json)
    {
        return JsonSerializer.Deserialize<T>(json, DefaultOptions);
    }

    private static JsonSerializerOptions CreateDefaultOptions()
    {
        return new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.Never,
            Converters =
        {
            new JsonStringEnumConverter()
        }
        };
    }
}