using System.Text;
using InstallBunker.Common.Serialization;

namespace InstallBunker.Common.Serialization;

public static class JsonFileStore
{
    public static void Save<T>(string filePath, T value)
    {
        var directory = Path.GetDirectoryName(filePath);

        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = InstallBunkerJson.Serialize(value);
        File.WriteAllText(filePath, json, Encoding.UTF8);
    }

    public static T Load<T>(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("JSON file not found.", filePath);
        }

        var json = File.ReadAllText(filePath, Encoding.UTF8);
        var result = InstallBunkerJson.Deserialize<T>(json);

        if (result is null)
        {
            throw new InvalidOperationException($"Failed to deserialize file '{filePath}' to type '{typeof(T).FullName}'.");
        }

        return result;
    }
}