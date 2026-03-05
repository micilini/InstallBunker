using InstallBunker.Common.Serialization;
using System.IO;

namespace InstallBunker.Builder.UI;

public static class BuilderRecentProjectsStore
{
    private const int MaxRecentProjects = 10;

    public static IReadOnlyList<string> Load()
    {
        var filePath = GetStorageFilePath();

        if (!File.Exists(filePath))
        {
            return Array.Empty<string>();
        }

        var data = InstallBunkerJson.Deserialize<List<string>>(File.ReadAllText(filePath));

        if (data is null)
        {
            return Array.Empty<string>();
        }

        return data
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static void Add(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
        {
            return;
        }

        var items = Load().ToList();

        items.RemoveAll(path => string.Equals(path, filePath, StringComparison.OrdinalIgnoreCase));
        items.Insert(0, filePath);

        var finalItems = items
            .Take(MaxRecentProjects)
            .ToList();

        Directory.CreateDirectory(Path.GetDirectoryName(GetStorageFilePath())!);
        File.WriteAllText(GetStorageFilePath(), InstallBunkerJson.Serialize(finalItems));
    }

    private static string GetStorageFilePath()
    {
        return InstallBunker.Common.Paths.InstallBunkerPaths.GetBuilderRecentProjectsFilePath();
    }
}