using InstallBunker.Common.Serialization;
using System.IO;

namespace InstallBunker.Builder.UI;

public static class BuilderProjectFileService
{
    public static BuilderProjectFile Load(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Project file was not found.", filePath);
        }

        var project = JsonFileStore.Load<BuilderProjectFile>(filePath);

        if (project.SchemaVersion <= 0)
        {
            throw new InvalidOperationException("Invalid .ibb schema version.");
        }

        return project;
    }

    public static void Save(string filePath, BuilderProjectFile project)
    {
        if (project is null)
        {
            throw new InvalidOperationException("Project cannot be null.");
        }

        if (string.IsNullOrWhiteSpace(filePath))
        {
            throw new InvalidOperationException("Project file path is required.");
        }

        JsonFileStore.Save(filePath, project);
    }
}